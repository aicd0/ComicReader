// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    /// <summary>
    /// Arranges child elements into a staggered grid pattern where items are added to the row that has used least amount of space.
    /// </summary>
    public class StaggeredPanelX : Panel
    {
        private double _columnWidth;
        private double _rowHeight;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaggeredPanelX"/> class.
        /// </summary>
        public StaggeredPanelX()
        {
            RegisterPropertyChangedCallback(Panel.HorizontalAlignmentProperty, OnHorizontalAlignmentChanged);
            RegisterPropertyChangedCallback(Panel.VerticalAlignmentProperty, OnVerticalAlignmentChanged);
        }

        /// <summary>
        /// Gets or sets the desired width for each column.
        /// </summary>
        /// <remarks>
        /// The width of columns can exceed the DesiredColumnWidth if the HorizontalAlignment is set to Stretch.
        /// </remarks>
        public double DesiredColumnWidth
        {
            get { return (double)GetValue(DesiredColumnWidthProperty); }
            set { SetValue(DesiredColumnWidthProperty, value); }
        }

        /// <summary>
        /// Gets or sets the desired height for each row.
        /// </summary>
        /// <remarks>
        /// The height of rows can exceed the DesiredRowHeight if the VerticalAlignment is set to Stretch.
        /// </remarks>
        public double DesiredRowHeight
        {
            get { return (double)GetValue(DesiredRowHeightProperty); }
            set { SetValue(DesiredRowHeightProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="DesiredColumnWidth"/> dependency property.
        /// </summary>
        /// <returns>The identifier for the <see cref="DesiredColumnWidth"/> dependency property.</returns>
        public static readonly DependencyProperty DesiredColumnWidthProperty = DependencyProperty.Register(
            nameof(DesiredColumnWidth),
            typeof(double),
            typeof(StaggeredPanel),
            new PropertyMetadata(250d, OnDesiredColumnWidthChanged));

        /// <summary>
        /// Identifies the <see cref="DesiredRowHeight"/> dependency property.
        /// </summary>
        /// <returns>The identifier for the <see cref="DesiredRowHeight"/> dependency property.</returns>
        public static readonly DependencyProperty DesiredRowHeightProperty = DependencyProperty.Register(
            nameof(DesiredRowHeight),
            typeof(double),
            typeof(StaggeredPanelX),
            new PropertyMetadata(250d, OnDesiredRowHeightChanged));

        /// <summary>
        /// Gets or sets the distance between the border and its child object.
        /// </summary>
        /// <returns>
        /// The dimensions of the space between the border and its child as a Thickness value.
        /// Thickness is a structure that stores dimension values using pixel measures.
        /// </returns>
        public Thickness Padding
        {
            get { return (Thickness)GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }

        /// <summary>
        /// Identifies the Padding dependency property.
        /// </summary>
        /// <returns>The identifier for the <see cref="Padding"/> dependency property.</returns>
        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(StaggeredPanelX),
            new PropertyMetadata(default(Thickness), OnPaddingChanged));

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="Orientation"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(StaggeredPanel),
            new PropertyMetadata(Orientation.Vertical, OnPaddingChanged));

        /// <summary>
        /// Gets or sets the spacing between columns of items.
        /// </summary>
        public double ColumnSpacing
        {
            get { return (double)GetValue(ColumnSpacingProperty); }
            set { SetValue(ColumnSpacingProperty, value); }
        }

        /// <summary>
        /// Gets or sets the spacing between rows of items.
        /// </summary>
        public double RowSpacing
        {
            get { return (double)GetValue(RowSpacingProperty); }
            set { SetValue(RowSpacingProperty, value); }
        }

        /// <summary>
        /// Identifies the <see cref="ColumnSpacing"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
            nameof(ColumnSpacing),
            typeof(double),
            typeof(StaggeredPanel),
            new PropertyMetadata(0d, OnPaddingChanged));

        /// <summary>
        /// Identifies the <see cref="RowSpacing"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
            nameof(RowSpacing),
            typeof(double),
            typeof(StaggeredPanelX),
            new PropertyMetadata(0d, OnPaddingChanged));

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            double availableHeight = availableSize.Height - Padding.Left - Padding.Right;
            double availableWidth = availableSize.Width - Padding.Top - Padding.Bottom;

            if (Orientation == Orientation.Vertical)
            {
                _columnWidth = Math.Min(DesiredColumnWidth, availableWidth);
                int numColumns = 1;//Math.Max(1, (int)Math.Floor(availableWidth / _columnWidth));

                // adjust for column spacing on all columns expect the first
                double totalWidth = _columnWidth + ((numColumns - 1) * (_columnWidth + ColumnSpacing));
                if (totalWidth > availableWidth)
                {
                    numColumns--;
                }
                else if (double.IsInfinity(availableWidth))
                {
                    availableWidth = totalWidth;
                }

                if (HorizontalAlignment == HorizontalAlignment.Stretch)
                {
                    availableWidth = availableWidth - ((numColumns - 1) * ColumnSpacing);
                    _columnWidth = availableWidth / numColumns;
                }

                if (Children.Count == 0)
                {
                    return new Size(0, 0);
                }

                var columnHeights = new double[numColumns];
                var itemsPerColumn = new double[numColumns];

                for (int i = 0; i < Children.Count; i++)
                {
                    var columnIndex = GetColumnIndex(columnHeights);

                    var child = Children[i];
                    child.Measure(new Size(_columnWidth, availableHeight));
                    var elementSize = child.DesiredSize;
                    columnHeights[columnIndex] += elementSize.Height + (itemsPerColumn[columnIndex] > 0 ? RowSpacing : 0);
                    itemsPerColumn[columnIndex]++;
                }

                double desiredHeight = columnHeights.Max();

                return new Size(availableWidth, desiredHeight);
            }
            else
            {
                _rowHeight = Math.Min(DesiredRowHeight, availableHeight);
                int numRows = 1;// Math.Max(1, (int)Math.Floor(availableHeight / _rowHeight));

                // adjust for row spacing on all rows expect the first
                double totalHeight = _rowHeight + ((numRows - 1) * (_rowHeight + RowSpacing));
                if (totalHeight > availableHeight)
                {
                    numRows--;
                }
                else if (double.IsInfinity(availableHeight))
                {
                    availableHeight = totalHeight;
                }

                if (VerticalAlignment == VerticalAlignment.Stretch)
                {
                    availableHeight = availableHeight - ((numRows - 1) * RowSpacing);
                    _rowHeight = availableHeight / numRows;
                }

                if (Children.Count == 0)
                {
                    return new Size(0, 0);
                }

                var rowWidths = new double[numRows];
                var itemsPerRow = new double[numRows];

                for (int i = 0; i < Children.Count; i++)
                {
                    var rowIndex = GetRowIndex(rowWidths);

                    var child = Children[i];
                    child.Measure(new Size(availableWidth, _rowHeight));
                    var elementSize = child.DesiredSize;
                    rowWidths[rowIndex] += elementSize.Width + (itemsPerRow[rowIndex] > 0 ? ColumnSpacing : 0);
                    itemsPerRow[rowIndex]++;
                }

                double desiredWidth = rowWidths.Max();

                return new Size(desiredWidth, availableHeight);
            }
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            double verticalOffset = Padding.Left;
            double horizontalOffset = Padding.Top;
            if (Orientation == Orientation.Vertical)
            {
                int numColumns = 1;// Math.Max(1, (int)Math.Floor(finalSize.Width / _columnWidth));

                // adjust for horizontal spacing on all columns expect the first
                double totalWidth = _columnWidth + ((numColumns - 1) * (_columnWidth + ColumnSpacing));
                if (totalWidth > finalSize.Width)
                {
                    numColumns--;

                    // Need to recalculate the totalWidth for a correct horizontal offset
                    totalWidth = _columnWidth + ((numColumns - 1) * (_columnWidth + ColumnSpacing));
                }

                if (HorizontalAlignment == HorizontalAlignment.Right)
                {
                    horizontalOffset += finalSize.Width - totalWidth;
                }
                else if (HorizontalAlignment == HorizontalAlignment.Center)
                {
                    horizontalOffset += (finalSize.Width - totalWidth) / 2;
                }

                var columnHeights = new double[numColumns];
                var itemsPerColumn = new double[numColumns];

                for (int i = 0; i < Children.Count; i++)
                {
                    var columnIndex = GetColumnIndex(columnHeights);

                    var child = Children[i];
                    var elementSize = child.DesiredSize;

                    double elementHeight = elementSize.Height;

                    double itemHorizontalOffset = horizontalOffset + (_columnWidth * columnIndex) + (ColumnSpacing * columnIndex);
                    double itemVerticalOffset = columnHeights[columnIndex] + verticalOffset + (RowSpacing * itemsPerColumn[columnIndex]);

                    Rect bounds = new Rect(itemHorizontalOffset, itemVerticalOffset, _columnWidth, elementHeight);
                    child.Arrange(bounds);

                    columnHeights[columnIndex] += elementSize.Height;
                    itemsPerColumn[columnIndex]++;
                }

                return base.ArrangeOverride(finalSize);
            }
            else
            {
                int numRows = 1;// Math.Max(1, (int)Math.Floor(finalSize.Height / _rowHeight));

                // adjust for vertical spacing on all rows expect the first
                double totalHeight = _rowHeight + ((numRows - 1) * (_rowHeight + RowSpacing));
                if (totalHeight > finalSize.Height)
                {
                    numRows--;

                    // Need to recalculate the totalHeight for a correct vertical offset
                    totalHeight = _rowHeight + ((numRows - 1) * (_rowHeight + RowSpacing));
                }

                if (VerticalAlignment == VerticalAlignment.Bottom)
                {
                    verticalOffset += finalSize.Height - totalHeight;
                }
                else if (VerticalAlignment == VerticalAlignment.Center)
                {
                    verticalOffset += (finalSize.Height - totalHeight) / 2;
                }

                var rowWidths = new double[numRows];
                var itemsPerRow = new double[numRows];

                for (int i = 0; i < Children.Count; i++)
                {
                    var rowIndex = GetRowIndex(rowWidths);

                    var child = Children[i];
                    var elementSize = child.DesiredSize;

                    double elementWidth = elementSize.Width;

                    double itemVerticalOffset = verticalOffset + (_rowHeight * rowIndex) + (RowSpacing * rowIndex);
                    double itemHorizontalOffset = rowWidths[rowIndex] + horizontalOffset + (ColumnSpacing * itemsPerRow[rowIndex]);

                    Rect bounds = new Rect(itemHorizontalOffset, itemVerticalOffset, elementWidth, _rowHeight);
                    child.Arrange(bounds);

                    rowWidths[rowIndex] += elementSize.Width;
                    itemsPerRow[rowIndex]++;
                }

                return base.ArrangeOverride(finalSize);
            }
        }

        private static void OnDesiredColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (StaggeredPanel)d;
            panel.InvalidateMeasure();
        }

        private static void OnDesiredRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (StaggeredPanelX)d;
            panel.InvalidateMeasure();
        }

        private static void OnPaddingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (StaggeredPanelX)d;
            panel.InvalidateMeasure();
        }

        private void OnHorizontalAlignmentChanged(DependencyObject sender, DependencyProperty dp)
        {
            InvalidateMeasure();
        }

        private void OnVerticalAlignmentChanged(DependencyObject sender, DependencyProperty dp)
        {
            InvalidateMeasure();
        }

        private int GetColumnIndex(double[] columnHeights)
        {
            int columnIndex = 0;
            double height = columnHeights[0];
            for (int j = 1; j < columnHeights.Length; j++)
            {
                if (columnHeights[j] < height)
                {
                    columnIndex = j;
                    height = columnHeights[j];
                }
            }

            return columnIndex;
        }

        private int GetRowIndex(double[] rowWidths)
        {
            int rowIndex = 0;
            double width = rowWidths[0];
            for (int j = 1; j < rowWidths.Length; j++)
            {
                if (rowWidths[j] < width)
                {
                    rowIndex = j;
                    width = rowWidths[j];
                }
            }

            return rowIndex;
        }
    }
}