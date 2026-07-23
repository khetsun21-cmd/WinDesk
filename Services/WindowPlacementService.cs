using Forms = System.Windows.Forms;

namespace WinDesk.Services;

public readonly record struct WindowWorkArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public readonly record struct WindowPlacement(double Left, double Top);

public sealed class WindowPlacementService
{
    public WindowPlacement ClampToVisibleArea(double left, double top, double width, double height)
    {
        var workAreas = GetCurrentWorkAreas();

        return ClampToVisibleArea(left, top, width, height, workAreas);
    }

    public WindowPlacement MoveProportionallyToVisibleArea(
        double left,
        double top,
        double width,
        double height,
        WindowWorkArea previousArea)
    {
        return MoveProportionallyToVisibleArea(left, top, width, height, previousArea, GetCurrentWorkAreas());
    }

    public WindowWorkArea? FindBestWorkArea(double left, double top, double width, double height)
    {
        return FindBestWorkArea(left, top, width, height, GetCurrentWorkAreas());
    }

    public static WindowPlacement ClampToVisibleArea(
        double left,
        double top,
        double width,
        double height,
        IEnumerable<WindowWorkArea> workAreas)
    {
        var areas = workAreas.Where(area => area.Width > 0 && area.Height > 0).ToArray();
        if (areas.Length == 0)
        {
            return new WindowPlacement(left, top);
        }

        var target = areas
            .OrderByDescending(area => IntersectionArea(left, top, width, height, area))
            .ThenBy(area => DistanceSquaredToCenter(left, top, width, height, area))
            .First();

        var maxLeft = target.Right - width;
        var maxTop = target.Bottom - height;
        var clampedLeft = Clamp(left, target.Left, Math.Max(target.Left, maxLeft));
        var clampedTop = Clamp(top, target.Top, Math.Max(target.Top, maxTop));

        return new WindowPlacement(clampedLeft, clampedTop);
    }

    public static WindowPlacement MoveProportionallyToVisibleArea(
        double left,
        double top,
        double width,
        double height,
        WindowWorkArea previousArea,
        IEnumerable<WindowWorkArea> currentWorkAreas)
    {
        var areas = currentWorkAreas.Where(area => area.Width > 0 && area.Height > 0).ToArray();
        if (areas.Length == 0)
        {
            return new WindowPlacement(left, top);
        }

        if (previousArea.Width <= 0 || previousArea.Height <= 0)
        {
            return ClampToVisibleArea(left, top, width, height, areas);
        }

        var target = areas
            .OrderBy(area => DistanceSquaredBetweenCenters(previousArea, area))
            .First();

        var ratioX = Ratio(left - previousArea.Left, previousArea.Width - width);
        var ratioY = Ratio(top - previousArea.Top, previousArea.Height - height);
        var mappedLeft = target.Left + ratioX * Math.Max(0, target.Width - width);
        var mappedTop = target.Top + ratioY * Math.Max(0, target.Height - height);

        return ClampToVisibleArea(mappedLeft, mappedTop, width, height, areas);
    }

    public static WindowWorkArea? FindBestWorkArea(
        double left,
        double top,
        double width,
        double height,
        IEnumerable<WindowWorkArea> workAreas)
    {
        var areas = workAreas.Where(area => area.Width > 0 && area.Height > 0).ToArray();
        if (areas.Length == 0)
        {
            return null;
        }

        return areas
            .OrderByDescending(area => IntersectionArea(left, top, width, height, area))
            .ThenBy(area => DistanceSquaredToCenter(left, top, width, height, area))
            .First();
    }

    private static IEnumerable<WindowWorkArea> GetCurrentWorkAreas()
    {
        return Forms.Screen.AllScreens.Select(screen =>
            new WindowWorkArea(
                screen.WorkingArea.Left,
                screen.WorkingArea.Top,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height));
    }

    private static double IntersectionArea(double left, double top, double width, double height, WindowWorkArea area)
    {
        var right = left + width;
        var bottom = top + height;
        var overlapWidth = Math.Max(0, Math.Min(right, area.Right) - Math.Max(left, area.Left));
        var overlapHeight = Math.Max(0, Math.Min(bottom, area.Bottom) - Math.Max(top, area.Top));

        return overlapWidth * overlapHeight;
    }

    private static double DistanceSquaredToCenter(double left, double top, double width, double height, WindowWorkArea area)
    {
        var windowCenterX = left + width / 2;
        var windowCenterY = top + height / 2;
        var areaCenterX = area.Left + area.Width / 2;
        var areaCenterY = area.Top + area.Height / 2;
        var dx = windowCenterX - areaCenterX;
        var dy = windowCenterY - areaCenterY;

        return dx * dx + dy * dy;
    }

    private static double DistanceSquaredBetweenCenters(WindowWorkArea a, WindowWorkArea b)
    {
        var ax = a.Left + a.Width / 2;
        var ay = a.Top + a.Height / 2;
        var bx = b.Left + b.Width / 2;
        var by = b.Top + b.Height / 2;
        var dx = ax - bx;
        var dy = ay - by;

        return dx * dx + dy * dy;
    }

    private static double Ratio(double offset, double range)
    {
        if (range <= 0)
        {
            return 0;
        }

        return Clamp(offset / range, 0, 1);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
