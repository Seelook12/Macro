using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Point = System.Windows.Point;

namespace Macro.Services
{
    public struct MatchResult
    {
        public Point? Point;
        public double Score;
    }

    public static class ImageSearchService
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Mat> _imageCache = 
            new System.Collections.Concurrent.ConcurrentDictionary<string, Mat>();

        /// <summary>
        /// 이미지 캐시를 비우고 할당된 메모리를 해제합니다.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var key in _imageCache.Keys)
            {
                if (_imageCache.TryRemove(key, out var mat))
                {
                    mat?.Dispose();
                }
            }
        }

        public static Point? FindImage(BitmapSource screenImage, string templatePath, double threshold, System.Windows.Rect? searchRegion = null)
        {
            return FindImageDetailed(screenImage, templatePath, threshold, searchRegion).Point;
        }

        public static MatchResult FindImageDetailed(BitmapSource screenImage, string templatePath, double threshold, System.Windows.Rect? searchRegion = null)
        {
            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                return new MatchResult { Point = null, Score = 0 };
            }

            try
            {
                using Mat screenMat = BitmapSourceConverter.ToMat(screenImage);
                if (screenMat.Empty()) return new MatchResult { Point = null, Score = 0 };

                using Mat screenMat3Channel = screenMat.Channels() == 4 
                    ? screenMat.CvtColor(ColorConversionCodes.BGRA2BGR) 
                    : screenMat.Clone();
                
                // 캐시에서 템플릿 이미지를 가져오거나 새로 로드
                if (!_imageCache.TryGetValue(templatePath, out var templateMat))
                {
                    templateMat = Cv2.ImRead(templatePath, ImreadModes.Color);
                    if (templateMat == null || templateMat.Empty()) return new MatchResult { Point = null, Score = 0 };
                    _imageCache[templatePath] = templateMat;
                }

                // 검색 대상 설정 (전체 vs ROI)
                Mat? roiMat = null;
                Mat sourceToSearch;

                if (searchRegion.HasValue)
                {
                    var r = searchRegion.Value;
                    int x = Math.Max(0, (int)r.X);
                    int y = Math.Max(0, (int)r.Y);
                    int w = Math.Min(screenMat3Channel.Width - x, (int)r.Width);
                    int h = Math.Min(screenMat3Channel.Height - y, (int)r.Height);

                    if (w > 0 && h > 0)
                    {
                        roiMat = screenMat3Channel.SubMat(new OpenCvSharp.Rect(x, y, w, h));
                        sourceToSearch = roiMat;
                    }
                    else
                    {
                        sourceToSearch = screenMat3Channel;
                    }
                }
                else
                {
                    sourceToSearch = screenMat3Channel;
                }

                try
                {
                    // 크기 검사
                    if (templateMat.Width > sourceToSearch.Width || templateMat.Height > sourceToSearch.Height)
                    {
                        return new MatchResult { Point = null, Score = 0 };
                    }

                    using Mat result = new Mat();
                    Cv2.MatchTemplate(sourceToSearch, templateMat, result, TemplateMatchModes.CCoeffNormed);
                    
                    Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                    if (maxVal >= threshold)
                    {
                        // ROI 오프셋 계산
                        int offsetX = (sourceToSearch == roiMat) ? (int)searchRegion!.Value.X : 0;
                        int offsetY = (sourceToSearch == roiMat) ? (int)searchRegion!.Value.Y : 0;

                        // 중심 좌표 계산
                        int centerX = maxLoc.X + (templateMat.Width / 2) + offsetX;
                        int centerY = maxLoc.Y + (templateMat.Height / 2) + offsetY;
                        return new MatchResult { Point = new Point(centerX, centerY), Score = maxVal };
                    }
                    else
                    {
                        return new MatchResult { Point = null, Score = maxVal };
                    }
                }
                finally
                {
                    roiMat?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image Match Failed: {ex.Message}");
            }

            return new MatchResult { Point = null, Score = 0 };
        }

        /// <summary>
        /// 지정된 영역의 평균 Gray 값을 계산합니다.
        /// </summary>
        public static double GetGrayAverage(System.Windows.Media.Imaging.BitmapSource screenImage, int x, int y, int width, int height)
        {
            if (screenImage == null) return 0;

            try
            {
                using Mat source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(screenImage);
                if (source.Empty()) return 0;

                using Mat gray = new Mat();
                // 1. 그레이스케일 변환
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

                // 2. ROI 설정 (이미지 범위를 벗어나지 않도록 보정)
                int safeX = Math.Max(0, Math.Min(x, gray.Cols - 1));
                int safeY = Math.Max(0, Math.Min(y, gray.Rows - 1));
                int safeW = Math.Max(1, Math.Min(width, gray.Cols - safeX));
                int safeH = Math.Max(1, Math.Min(height, gray.Rows - safeY));

                using Mat roi = new Mat(gray, new OpenCvSharp.Rect(safeX, safeY, safeW, safeH));
                // 3. 평균값 반환
                return roi.Mean().Val0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
