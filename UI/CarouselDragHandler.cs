using System.Windows;
using System.Windows.Controls;
using Point = System.Windows.Point;

namespace fflauncher.UI
{
    public class CarouselDragHandler
    {
        private bool _isDragging;
        private bool _maybeDragging;
        private Point _start;
        private double _offset;
        public bool SuppressClick { get; private set; }
        private const double DragThreshold = 12.0; // 🔥 important for Wine/Android

        public void MouseDown(ScrollViewer sv, Point point)
        {
            _start = point;
            _offset = sv.HorizontalOffset;

            _maybeDragging = true;
            _isDragging = false;
        }

        public void MouseMove(ScrollViewer sv, Point current)
        {
            if (!_maybeDragging && !_isDragging)
                return;

            double deltaX = Math.Abs(current.X - _start.X);

            // 🔥 only start dragging AFTER threshold
            if (!_isDragging)
            {
                if (deltaX < DragThreshold)
                    return;

                _isDragging = true;
                sv.CaptureMouse();

                SuppressClick = true; // 🔥 ADD THIS
            }

            double delta = _start.X - current.X;
            sv.ScrollToHorizontalOffset(_offset + delta * 0.02);
        }

        public bool MouseUp(ScrollViewer sv)
        {
            bool wasDragging = _isDragging;

            if (_isDragging)
                sv.ReleaseMouseCapture();

            _isDragging = false;
            _maybeDragging = false;

            SuppressClick = false; // 🔥 ADD THIS

            return wasDragging;
        }
    }
}