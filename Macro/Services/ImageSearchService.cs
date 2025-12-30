using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Point = System.Windows.Point;

namespace Macro.Services
{
    public static class ImageSearchService
    {
        public static Point? FindImage(BitmapSource screenImage, string templatePath, double threshold, System.Windows.Rect? searchRegion = null)
        {
            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                return null;
            }

            try
            {
                using Mat screenMat = BitmapSourceConverter.ToMat(screenImage);
                using Mat screenMat3Channel = screenMat.Channels() == 4 ? screenMat.CvtColor(ColorConversionCodes.BGRA2BGR) : screenMat;
                
                using Mat templateMat = Cv2.ImRead(templatePath, ImreadModes.Color);
                if (templateMat.Empty()) return null;

                // 검색 대상 설정 (전체 vs ROI)
                Mat sourceToSearch = screenMat3Channel;
                Mat? roiMat = null;
                int roiOffsetX = 0;
                int roiOffsetY = 0;

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
                        roiOffsetX = x;
                        roiOffsetY = y;
                    }
                }

                // 크기 검사
                if (templateMat.Width > sourceToSearch.Width || templateMat.Height > sourceToSearch.Height)
                {
                    roiMat?.Dispose(); // ROI 사용했다면 해제
                    return null;
                }

                using Mat result = new Mat();
                Cv2.MatchTemplate(sourceToSearch, templateMat, result, TemplateMatchModes.CCoeffNormed);
                
                Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

                // 사용한 ROI 리소스 해제
                roiMat?.Dispose();

                if (maxVal >= threshold)
                {
                    // 중심 좌표 계산 (ROI 오프셋 추가)
                    int centerX = maxLoc.X + (templateMat.Width / 2) + roiOffsetX;
                    int centerY = maxLoc.Y + (templateMat.Height / 2) + roiOffsetY;
                    return new Point(centerX, centerY);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image Match Failed: {ex.Message}");
            }

            return null;
        }
        /// <summary>
        /// 지정된 영역의 평균 Gray 값을 계산합니다.
        /// </summary>
        public static double GetGrayAverage(System.Windows.Media.Imaging.BitmapSource screenImage, int x, int y, int width, int height)
        {
            if (screenImage == null) return 0;

            using (Mat source = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(screenImage))
            using (Mat gray = new Mat())
            {
                // 1. 그레이스케일 변환
                Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

                // 2. ROI 설정 (이미지 범위를 벗어나지 않도록 보정)
                int safeX = Math.Max(0, Math.Min(x, gray.Cols - 1));
                int safeY = Math.Max(0, Math.Min(y, gray.Rows - 1));
                int safeW = Math.Max(1, Math.Min(width, gray.Cols - safeX));
                int safeH = Math.Max(1, Math.Min(height, gray.Rows - safeY));

                using (Mat roi = new Mat(gray, new OpenCvSharp.Rect(safeX, safeY, safeW, safeH)))
                {
                    // 3. 평균값 반환
                    return roi.Mean().Val0;
                }
            }
        }
    }
}