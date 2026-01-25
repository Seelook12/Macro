using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Macro.Utils
{
    public class UriToBitmapConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                try
                {
                    string fullPath = path;

                    // 상대 경로 처리
                    if (!Path.IsPathRooted(path))
                    {
                        var currentRecipe = RecipeManager.Instance.CurrentRecipe;
                        if (currentRecipe != null && !string.IsNullOrEmpty(currentRecipe.FilePath))
                        {
                            var dir = Path.GetDirectoryName(currentRecipe.FilePath);
                            if (dir != null)
                            {
                                fullPath = Path.Combine(dir, path);
                            }
                        }
                    }

                    if (File.Exists(fullPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // 메모리에 로드 후 파일 해제
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // 캐시 무시하고 매번 새로 로드
                        bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze(); // 다른 스레드 접근 허용
                        return bitmap;
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
