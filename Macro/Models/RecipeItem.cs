namespace Macro.Models
{
    public class RecipeItem
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        // 리스트박스에 표시될 때 파일명만 보이도록 ToString 오버라이드
        public override string ToString()
        {
            return FileName;
        }
    }
}
