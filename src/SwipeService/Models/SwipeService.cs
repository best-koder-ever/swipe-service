namespace SwipeService.Models
{
    public class SwipeRequest
    {
        public int UserId { get; set; }
        public int TargetUserId { get; set; }
        public bool IsLike { get; set; }
    }
}