﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Lighthouse.Utilities
{
    [ValueConversion(typeof(Color), typeof(string))]
    public class ColorToHexConverter : IValueConverter
    {
        /// <summary>
        /// Converts a value.
        /// </summary>
        /// <param name="value">The value produced by the binding source.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A converted value. If the method returns <c>null</c>, the valid <c>null</c> value is used.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var color = (Color?) value;
            return color?.ColorToHex();
        }

        /// <summary>
        /// Converts a value.
        /// </summary>
        /// <param name="value">The value that is produced by the binding target.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A converted value. If the method returns <c>null</c>, the valid <c>null</c> value is used.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ColorHelper.HexToColor((string)value);
        }
    }

    [ValueConversion(typeof(Color), typeof(SolidColorBrush))]
    public class ColorToBrushConverter : IValueConverter
    {
        [SuppressMessage("ReSharper", "CanBeReplacedWithTryCastAndCheckForNull")]
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (value == null)
            {
                return underlyingType != null ? null : DependencyProperty.UnsetValue;
            }

            if (typeof(Brush).IsAssignableFrom(targetType))
            {
                if (value is Color)
                {
                    return new SolidColorBrush((Color)value);
                }
            }

            if (targetType == typeof(Color) || underlyingType == typeof(Color))
            {
                if (value is SolidColorBrush)
                {
                    return ((SolidColorBrush)value).Color;
                }
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}