using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinTaskbar.Controls
{
    public class CircularGauge : Control
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularGauge),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeBrushProperty =
            DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(CircularGauge),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(CircularGauge),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(CircularGauge),
                new FrameworkPropertyMetadata(2.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public Brush StrokeBrush
        {
            get => (Brush)GetValue(StrokeBrushProperty);
            set => SetValue(StrokeBrushProperty, value);
        }

        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double size = Math.Min(ActualWidth, ActualHeight);
            if (size <= StrokeThickness) return;

            double radius = (size - StrokeThickness) / 2.0;
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);

            // 1. Draw Background Track Circle
            var trackPen = new Pen(TrackBrush, StrokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawEllipse(null, trackPen, center, radius, radius);

            // 2. Draw Progress Arc
            double pct = Math.Clamp(Value / 100.0, 0.0, 1.0);
            if (pct <= 0.005) return;

            if (pct >= 0.999)
            {
                var fullPen = new Pen(StrokeBrush, StrokeThickness)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                dc.DrawEllipse(null, fullPen, center, radius, radius);
                return;
            }

            double angle = pct * 360.0;
            double rad = (angle - 90.0) * Math.PI / 180.0;
            Point startPoint = new Point(center.X, center.Y - radius);
            Point endPoint = new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(startPoint, false, false);
                ctx.ArcTo(endPoint, new Size(radius, radius), 0, angle > 180.0, SweepDirection.Clockwise, true, false);
            }

            var strokePen = new Pen(StrokeBrush, StrokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawGeometry(null, strokePen, geometry);
        }
    }
}
