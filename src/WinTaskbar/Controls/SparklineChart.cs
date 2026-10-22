using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace WinTaskbar.Controls
{
    public class SparklineChart : FrameworkElement
    {
        public static readonly DependencyProperty PrimaryValuesProperty =
            DependencyProperty.Register(nameof(PrimaryValues), typeof(IReadOnlyList<double>), typeof(SparklineChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondaryValuesProperty =
            DependencyProperty.Register(nameof(SecondaryValues), typeof(IReadOnlyList<double>), typeof(SparklineChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PrimaryBrushProperty =
            DependencyProperty.Register(nameof(PrimaryBrush), typeof(Brush), typeof(SparklineChart),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 183, 195)), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SecondaryBrushProperty =
            DependencyProperty.Register(nameof(SecondaryBrush), typeof(Brush), typeof(SparklineChart),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 69, 58)), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SparklineChart),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AutoScaleProperty =
            DependencyProperty.Register(nameof(AutoScale), typeof(bool), typeof(SparklineChart),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public IReadOnlyList<double>? PrimaryValues
        {
            get => (IReadOnlyList<double>?)GetValue(PrimaryValuesProperty);
            set => SetValue(PrimaryValuesProperty, value);
        }

        public IReadOnlyList<double>? SecondaryValues
        {
            get => (IReadOnlyList<double>?)GetValue(SecondaryValuesProperty);
            set => SetValue(SecondaryValuesProperty, value);
        }

        public Brush PrimaryBrush
        {
            get => (Brush)GetValue(PrimaryBrushProperty);
            set => SetValue(PrimaryBrushProperty, value);
        }

        public Brush SecondaryBrush
        {
            get => (Brush)GetValue(SecondaryBrushProperty);
            set => SetValue(SecondaryBrushProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public bool AutoScale
        {
            get => (bool)GetValue(AutoScaleProperty);
            set => SetValue(AutoScaleProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0) return;

            // Background subtle grid line
            var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
            dc.DrawLine(guidePen, new Point(0, height * 0.5), new Point(width, height * 0.5));

            if (SecondaryValues != null && SecondaryValues.Count > 1)
            {
                // If dual values (e.g. Read/Write or Down/Up), split height in half or overlay
                DrawSeries(dc, PrimaryValues, PrimaryBrush, 0, height * 0.5, true);
                DrawSeries(dc, SecondaryValues, SecondaryBrush, height * 0.5, height * 0.5, true);
            }
            else
            {
                DrawSeries(dc, PrimaryValues, PrimaryBrush, 0, height, false);
            }
        }

        private void DrawSeries(DrawingContext dc, IReadOnlyList<double>? values, Brush strokeBrush, double top, double plotHeight, bool isSplit)
        {
            if (values == null || values.Count < 2) return;

            double max = MaxValue;
            if (AutoScale || max <= 0)
            {
                max = 1;
                foreach (var v in values)
                {
                    if (v > max) max = v;
                }
                max *= 1.15; // padding
            }

            double stepX = ActualWidth / Math.Max(1, values.Count - 1);
            var points = new Point[values.Count];

            for (int i = 0; i < values.Count; i++)
            {
                double normalized = Math.Clamp(values[i] / max, 0, 1);
                double y = (top + plotHeight) - (normalized * (plotHeight - 4));
                points[i] = new Point(i * stepX, y);
            }

            // Create Area Path
            var areaGeometry = new StreamGeometry();
            using (var ctx = areaGeometry.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, top + plotHeight), true, true);
                ctx.LineTo(points[0], true, false);

                for (int i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i], true, false);
                }

                ctx.LineTo(new Point(points[^1].X, top + plotHeight), true, false);
            }
            areaGeometry.Freeze();

            Color baseColor = strokeBrush is SolidColorBrush scb ? scb.Color : Color.FromRgb(0, 183, 195);
            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(140, baseColor.R, baseColor.G, baseColor.B),
                Color.FromArgb(15, baseColor.R, baseColor.G, baseColor.B),
                new Point(0, 0), new Point(0, 1));
            fillBrush.Freeze();

            dc.DrawGeometry(fillBrush, null, areaGeometry);

            // Draw line
            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                ctx.BeginFigure(points[0], false, false);
                for (int i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i], true, false);
                }
            }
            lineGeometry.Freeze();

            var pen = new Pen(strokeBrush, 1.5);
            pen.Freeze();
            dc.DrawGeometry(null, pen, lineGeometry);
        }
    }
}
