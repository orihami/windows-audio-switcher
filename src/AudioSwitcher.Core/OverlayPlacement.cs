namespace AudioSwitcher.Core;

public readonly record struct Rect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

/// <summary>選択UIを置く場所を決める(画面の大きさ・作業領域はアプリから渡す)。</summary>
public static class OverlayPlacement
{
    /// <summary>
    /// workArea: タスクバーを除いた画面の範囲。右下なら、時計・通知領域の少し上(タスクバーが下にあるとき)になる。
    /// タスクバーが横や上にあっても、作業領域の角に置くので重ならない。
    /// </summary>
    public static (double X, double Y) Place(Rect workArea, double width, double height, OverlayCorner corner, double margin)
    {
        double left = workArea.Left + margin;
        double right = workArea.Right - margin - width;
        double top = workArea.Top + margin;
        double bottom = workArea.Bottom - margin - height;
        var (x, y) = corner switch
        {
            OverlayCorner.BottomRight => (right, bottom),
            OverlayCorner.BottomLeft => (left, bottom),
            OverlayCorner.TopRight => (right, top),
            OverlayCorner.TopLeft => (left, top),
            _ => (workArea.Left + (workArea.Width - width) / 2, workArea.Top + (workArea.Height - height) / 2),
        };
        // 画面からはみ出さない(大きすぎるときは左上をそろえる)
        x = Math.Max(workArea.Left, Math.Min(x, workArea.Right - width));
        y = Math.Max(workArea.Top, Math.Min(y, workArea.Bottom - height));
        return (x, y);
    }
}
