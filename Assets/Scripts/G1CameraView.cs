public enum G1CameraView : byte
{
    Rgb = 0,
    Depth = 1,
    Overlay = 2,
    Near = 3,
    Disparity = 4,
    PointCloud = 5,
    TopDown = 6
}

public static class G1CameraViewInfo
{
    public const int Count = 7;

    public const ushort ValidMask =
        (1 << Count) - 1;

    public static ushort Bit(G1CameraView view)
    {
        return (ushort)(1 << (int)view);
    }

    public static bool IsValid(int viewId)
    {
        return viewId >= 0 &&
               viewId < Count;
    }

    public static string Label(G1CameraView view)
    {
        switch (view)
        {
            case G1CameraView.Rgb:
                return "RGB";

            case G1CameraView.Depth:
                return "DEPTH";

            case G1CameraView.Overlay:
                return "OVERLAY";

            case G1CameraView.Near:
                return "NEAR";

            case G1CameraView.Disparity:
                return "DISPARITY";

            case G1CameraView.PointCloud:
                return "POINT CLOUD";

            case G1CameraView.TopDown:
                return "TOP DOWN";

            default:
                return "UNKNOWN";
        }
    }
}