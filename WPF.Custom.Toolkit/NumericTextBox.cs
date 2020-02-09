using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPF.Custom.Toolkit
{
    public class NumericTextBox : TextBox
    {
        private CultureInfo SpecificCulture => Language.GetSpecificCulture();

        #region Properties

        public static readonly DependencyProperty RealValueProperty =
            DependencyProperty.Register(nameof(RealValue), typeof(double?), typeof(NumericTextBox),
                new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnRealValueChanged));

        [Bindable(true)]
        [Category("Common")]
        [DefaultValue(null)]
        public double? RealValue
        {
            get => (double?)GetValue(RealValueProperty);
            set => SetValue(RealValueProperty, value);
        }

        public static readonly DependencyProperty StringFormatProperty =
            DependencyProperty.Register(nameof(StringFormat), typeof(string), typeof(NumericTextBox),
                new FrameworkPropertyMetadata(string.Empty, OnStringFormatChanged));

        [Bindable(true)]
        [Category("Common")]
        [DefaultValue(null)]
        public string StringFormat
        {
            get => (string)GetValue(StringFormatProperty);
            set => SetValue(StringFormatProperty, value);
        }

        #endregion

        #region Events

        public static readonly RoutedEvent RealValueChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(RealValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<double?>),
            typeof(NumericTextBox));

        public event RoutedPropertyChangedEventHandler<double?> RealValueChanged
        {
            add => AddHandler(RealValueChangedEvent, value);
            remove => RemoveHandler(RealValueChangedEvent, value);
        }

        #endregion

        #region Overrides

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            VerticalContentAlignment = VerticalAlignment.Center;
            HorizontalContentAlignment = HorizontalAlignment.Right;

            DataObject.AddPastingHandler(this, OnTextPasted);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            if (string.IsNullOrEmpty(Text))
            {
                InternalSetText(RealValue);
            }
            else
            {
                var (outcome, conversion) = Text.TryParse(SpecificCulture);
                if (outcome && !IsReadOnly)
                {
                    SetValue(RealValueProperty, conversion);
                }
            }

            e.Handled = true;
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            base.OnPreviewTextInput(e);

            var control = (TextBox)e.OriginalSource;
            var previewCompleteText = control.Text.Remove(control.SelectionStart, control.SelectionLength)
                .Insert(control.CaretIndex, e.Text);

            var (outcome, _) = previewCompleteText.TryParse(SpecificCulture);
            e.Handled = !outcome;
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);

            if (e.UndoAction != UndoAction.Undo &&
                e.UndoAction != UndoAction.Redo)
            {
                return;
            }

            var control = (TextBox)e.OriginalSource;
            var (outcome, conversion) = control.Text.TryParse(SpecificCulture);
            if (outcome)
            {
                SetCurrentValue(RealValueProperty, conversion);
            }
        }

        #endregion

        #region Details

        private static void OnRealValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NumericTextBox)d;
            control.OnRealValueChanged((double?)e.OldValue, (double?)e.NewValue);
        }

        private void OnRealValueChanged(double? oldValue, double? newValue)
        {
            if (!newValue.HasValue)
            {
                return;
            }

            if (newValue.EqualTo(oldValue, double.Epsilon))
            {
                return;
            }

            RaiseEvent(new RoutedPropertyChangedEventArgs<double?>(oldValue, newValue, RealValueChangedEvent));
            InternalSetText(newValue);
        }

        private void OnTextPasted(object sender, DataObjectPastingEventArgs e)
        {
            var control = (TextBox)sender;
            var currentText = control.Text;

            var isText = e.SourceDataObject.GetDataPresent(DataFormats.Text, true);
            if (!isText)
            {
                e.CancelCommand();
                return;
            }

            var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string;
            var previewCompleteText =
                string.Concat(currentText.Substring(0, control.SelectionStart), pastedText,
                    currentText.Substring(control.SelectionStart + control.SelectionLength));

            var (outcome, _) = previewCompleteText.TryParse(SpecificCulture);
            if (!outcome)
            {
                e.CancelCommand();
            }
        }

        private static void OnStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (string.Equals((string)e.OldValue, (string)e.NewValue, StringComparison.Ordinal))
            {
                return;
            }

            var control = (NumericTextBox)d;
            control.InternalSetText(control.RealValue);
        }

        private void InternalSetText(double? newRealValue)
        {
            if (newRealValue.HasValue && !IsReadOnly)
            {
                SetValue(TextProperty, newRealValue.Value.ToString(StringFormat, SpecificCulture));
            }
        }

        #endregion
    }

    public static class DoubleExtensions
    {
        public static bool EqualTo(this double lhs, double rhs, double criteria = 1e-16) =>
            Math.Abs(lhs - rhs) <= criteria;

        public static bool EqualTo(this double? lhs, double? rhs, double criteria) =>
            lhs.HasValue && rhs.HasValue && EqualTo(lhs.Value, rhs.Value, criteria);
    }

    public static class NumericParser
    {
        public static (bool outcome, double conversion) TryParse(this string text, CultureInfo culture)
        {
            if (text.Count(c => c == culture.NumberFormat.PositiveSign[0]) > 1 ||
                text.Count(c => c == culture.NumberFormat.NegativeSign[0]) > 1)
            {
                return (false, double.NaN);
            }

            if (text == culture.NumberFormat.PositiveSign ||
                text == culture.NumberFormat.NegativeSign)
            {
                return (true, double.NaN);
            }

            if (text.Any() &&
                (text.Last() == culture.NumberFormat.PositiveSign[0] ||
                 text.Last() == culture.NumberFormat.NegativeSign[0]))
            {
                return (false, double.NaN);
            }

            return Parse(text, culture);
        }

        public static (bool outcome, double conversion) Parse(this string number, CultureInfo culture) =>
            double.TryParse(number, NumberStyles.Number | NumberStyles.Integer | NumberStyles.Float, culture,
                out var conversion)
                ? (true, conversion)
                : (false, conversion);
    }
}
