using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace fflauncher.UI
{
    public class CarouselDragHandler
    {
        private bool _isDragging;
        private Point _start;
        private double _offset;

        public void MouseDown(ScrollViewer sv, Point point)
        {
            _start = point;
            _offset = sv.HorizontalOffset;
            _isDragging = true;
        }

        public void MouseMove(ScrollViewer sv, Point current)
        {
            if (!_isDragging) return;

            double delta = _start.X - current.X;
            sv.ScrollToHorizontalOffset(_offset + delta * 3);
        }

        public void MouseUp()
        {
            _isDragging = false;
        }
    }
}
