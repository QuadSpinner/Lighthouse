using Lighthouse.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Lighthouse.Core
{
    public class Highlighter
    {
        private IAdornmentLayer adornmentLayer;
        private BlurType isBlurred = BlurType.Selective;
        private IWpfTextView textView;

        public Highlighter(IWpfTextView view)
        {
            if (Lighthouse.Options == null)
                Lighthouse.Init();

            textView = view;
            adornmentLayer = view.GetAdornmentLayer("LighthouseHighlighter");

            textView.LayoutChanged += OnLayoutChanged;

            if (!File.Exists(LighthouseOptions.localPath + LighthouseOptions.settingsFile))
            {
                Lighthouse.Init();
                Lighthouse.Options.SaveSettingsToStorage();
            }

            FileSystemWatcher fsw = new FileSystemWatcher(LighthouseOptions.localPath, LighthouseOptions.settingsFile);
            fsw.Changed += Fsw_Changed;
        }

        private void Fsw_Changed(object sender, FileSystemEventArgs e)
        {
            Lighthouse.Init();

            adornmentLayer.RemoveAllAdornments();

            foreach (ITextViewLine line in textView.TextViewLines.Where(x => x.VisibilityState == VisibilityState.FullyVisible))
            {
                CreateVisuals(line);
            }
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs args)
        {
            if (Lighthouse.Options == null)
            {
                Lighthouse.Init();
            }

            if (!Lighthouse.IsEnabled)
                return;

            try
            {
                foreach (ITextViewLine line in args.NewOrReformattedLines)
                {
                    CreateVisuals(line);
                }
            }
            catch (Exception)
            {
                //MessageBox.Show(ex.Message);
            }
        }

        [SuppressMessage("ReSharper", "SwitchStatementMissingSomeCases")]
        private void CreateVisuals(ITextViewLine line)
        {
            // Grab a reference to the lines in the current TextView
            IWpfTextViewLineCollection textViewLines = textView.TextViewLines;
            int start = line.Start;
            int end = line.End;
            List<Geometry> geometries = new List<Geometry>();

            var keywordFirstLetters = Lighthouse.keywordFormats.Keys
                                                    .Select(k => k[0])
                                                    .Distinct()
                                                    .ToArray();
            char[] escapes =
            {
                ' ',
                '!',
                '"',
                '@',
                '$',
                '(',
                ')',
                '[',
                ']',
                '*',
                '-',
                '.',
                '/',
                '>',
                '<',
                '"',
                ':',
                ';',
                ',',
                '?',
                '\'',
                '\n',
                '\r',
                '\t',
                '='
            };
            isBlurred = Lighthouse.Options.Blurred;

            // ~~ Main Loop
            for (int i = start; i < end; i++)
            {
                if (keywordFirstLetters.Contains(textView.TextSnapshot[i]))
                {
                    foreach (var kvp in Lighthouse.keywordFormats)
                    {
                        string keyword = kvp.Key.Trim();

                        if (textView.TextSnapshot[i] == keyword[0] &&
                            i <= end - keyword.Length &&
                            textView.TextSnapshot.GetText(i, keyword.Length) == keyword &&
                            escapes.Contains(Convert.ToChar(textView.TextSnapshot.GetText(i - 1, 1))) &&
                            escapes.Contains(Convert.ToChar(textView.TextSnapshot.GetText(i + keyword.Length, 1)))
                        )
                        {
                            SnapshotSpan span =
                                new SnapshotSpan(textView.TextSnapshot, Span.FromBounds(i, i + keyword.Length));

                            Thickness t = new Thickness(2, 0, 2, 0);

                            switch (isBlurred)
                            {
                                case BlurType.BlurAll:
                                    t = new Thickness(2, -3, 2, -3);

                                    break;

                                case BlurType.Selective:
                                    t = new Thickness(2,
                                                      kvp.Value.Blur != BlurIntensity.None ? -3 : 0,
                                                      2,
                                                      kvp.Value.Blur != BlurIntensity.None ? -3 : 0);
                                    break;
                            }

                            Geometry markerGeometry = textViewLines.GetMarkerGeometry(span, true, t);
                            if (markerGeometry != null)
                            {
                                if (!geometries.Any(g => g.FillContainsWithDetail(markerGeometry) >
                                                         IntersectionDetail.Empty))
                                {
                                    geometries.Add(markerGeometry);
                                    AddMarker(span,
                                              markerGeometry,
                                              kvp.Value);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddMarker(SnapshotSpan span,
                               Geometry markerGeometry,
                               ColorTag ct)
        {
            double cornerRadius;

            try
            {
                switch (Lighthouse.Options.HighlightCorner)
                {
                    case CornerStyle.Square:
                        cornerRadius = 0.0;
                        break;

                    case CornerStyle.RoundedCorner:
                        cornerRadius = 2.0;
                        break;

                    case CornerStyle.Soft:
                        cornerRadius = 6.0;
                        break;

                    default:
                        cornerRadius = 2.0;
                        break;
                }
            }
            catch (Exception)
            {
                cornerRadius = 2.0;
            }

            Rectangle r = new Rectangle
            {
                Fill = new SolidColorBrush(ct.ColorSwatch.ChangeAlpha(60)),
                RadiusX = cornerRadius,
                RadiusY = cornerRadius,
                Width = markerGeometry.Bounds.Width,
                Height = ct.isUnderline ? 4 : markerGeometry.Bounds.Height,
                Stroke = new SolidColorBrush(ct.ColorSwatch.ChangeAlpha(100))
            };

            if (ct.isFullLine)
            {
                r.Width = textView.ViewportWidth - markerGeometry.Bounds.Left;
            }

            if (isBlurred != BlurType.NoBlur)
            {
                if (isBlurred == BlurType.BlurAll)
                    ct.Blur = Lighthouse.Options.Blur;

                if (ct.Blur != BlurIntensity.None)
                {
                    r.Effect = new BlurEffect
                    {
                        KernelType = KernelType.Gaussian,
                        RenderingBias = RenderingBias.Performance
                    };
                    bool isLine = ct.isUnderline;
                    switch (ct.Blur)
                    {
                        case BlurIntensity.Low:
                            ((SolidColorBrush)r.Fill).Color.ChangeAlpha(80);
                            ((BlurEffect)r.Effect).Radius = isLine ? 2 : 4.0;
                            break;

                        case BlurIntensity.Medium:
                            ((SolidColorBrush)r.Fill).Color.ChangeAlpha(120);
                            ((BlurEffect)r.Effect).Radius = isLine ? 4 : 7.0;
                            break;

                        case BlurIntensity.High:
                            ((SolidColorBrush)r.Fill).Color.ChangeAlpha(170);
                            ((BlurEffect)r.Effect).Radius = isLine ? 6 : 11.0;
                            break;

                        case BlurIntensity.Ultra:
                            ((SolidColorBrush)r.Fill).Color.ChangeAlpha(255);
                            ((BlurEffect)r.Effect).Radius = isLine ? 8 : 20.0;
                            break;

                        default:
                        case BlurIntensity.None:
                            r.Effect = null;
                            break;
                    }

                    r.Stroke = null;
                }
            }

            // Align the image with the top of the bounds of the text geometry
            Canvas.SetLeft(r, markerGeometry.Bounds.Left);
            Canvas.SetTop(r, ct.isUnderline ? (markerGeometry.Bounds.Top + markerGeometry.Bounds.Height - 2) : markerGeometry.Bounds.Top);

            adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, span, null, r, null);
        }
    }
}