using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GeFeSLE.Controls
{
    public class RichHtmlControl : UserControl
    {
        public static readonly StyledProperty<string> HtmlContentProperty =
            AvaloniaProperty.Register<RichHtmlControl, string>(nameof(HtmlContent), string.Empty);

        private StackPanel? _contentPanel;

        public string HtmlContent
        {
            get => GetValue(HtmlContentProperty);
            set => SetValue(HtmlContentProperty, value);
        }

        public RichHtmlControl()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == HtmlContentProperty)
            {
                UpdateContent();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Simple approach - just let the base handle measurement with ClipToBounds=true
            var result = base.MeasureOverride(availableSize);
            
            // Ensure we never exceed the available width
            if (!double.IsInfinity(availableSize.Width))
            {
                result = result.WithWidth(Math.Min(result.Width, availableSize.Width));
            }
            
            return result;
        }

        private void InitializeComponent()
        {
            // Create the main layout with proper width constraints
            _contentPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                Spacing = 8
            };

            // No additional ScrollViewer here since the parent XAML already has one
            Content = _contentPanel;

            // Ensure the control respects container width and doesn't overflow
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            
            // Ensure we don't exceed container bounds
            ClipToBounds = true;
        }

        private void UpdateContent()
        {
            if (_contentPanel == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                _contentPanel.Children.Clear();

                if (string.IsNullOrWhiteSpace(HtmlContent))
                    return;

                try
                {
                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(HtmlContent);

                    ProcessHtmlNode(htmlDoc.DocumentNode, _contentPanel);
                }
                catch (Exception ex)
                {
                    // Fallback to plain text if HTML parsing fails
                    var errorText = new TextBlock
                    {
                        Text = $"Error parsing HTML: {ex.Message}\n\nRaw content:\n{HtmlContent}",
                        Foreground = Brushes.Red,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    _contentPanel.Children.Add(errorText);
                }
            });
        }

        private void ProcessHtmlNode(HtmlNode node, Panel container)
        {
            foreach (var child in node.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case HtmlNodeType.Text:
                        var text = child.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            var textBlock = new TextBlock
                            {
                                Text = text,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = Brushes.White,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                                MaxWidth = double.PositiveInfinity // Will be constrained by MeasureOverride
                            };
                            container.Children.Add(textBlock);
                        }
                        break;

                    case HtmlNodeType.Element:
                        ProcessHtmlElement(child, container);
                        break;
                }
            }
        }

        private void ProcessHtmlElement(HtmlNode element, Panel container)
        {
            switch (element.Name.ToLower())
            {
                case "p":
                    var paragraph = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8),
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(paragraph);
                    break;

                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    var heading = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        FontWeight = FontWeight.Bold,
                        FontSize = GetHeadingSize(element.Name),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 8, 0, 8),
                        Foreground = Brushes.LightBlue,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    container.Children.Add(heading);
                    break;

                case "strong":
                case "b":
                    var bold = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        FontWeight = FontWeight.Bold,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    container.Children.Add(bold);
                    break;

                case "em":
                case "i":
                    var italic = new TextBlock
                    {
                        Text = element.InnerText?.Trim() ?? "",
                        FontStyle = FontStyle.Italic,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    container.Children.Add(italic);
                    break;

                case "a":
                    var href = element.GetAttributeValue("href", "");
                    var linkText = element.InnerText?.Trim() ?? href;
                    
                    var link = new Button
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = Brushes.LightBlue,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Padding = new Thickness(0),
                        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                        Content = new TextBlock
                        {
                            Text = linkText,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.LightBlue,
                            TextDecorations = TextDecorations.Underline,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                        }
                    };
                    
                    if (!string.IsNullOrEmpty(href))
                    {
                        link.Click += (s, e) => OpenUrl(href);
                    }
                    
                    container.Children.Add(link);
                    break;

                case "ul":
                    var ulPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        Margin = new Thickness(20, 4, 0, 4),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    ProcessListItems(element, ulPanel, "• ");
                    container.Children.Add(ulPanel);
                    break;

                case "ol":
                    var olPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        Margin = new Thickness(20, 4, 0, 4),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    ProcessOrderedListItems(element, olPanel);
                    container.Children.Add(olPanel);
                    break;

                case "code":
                    var code = new TextBlock
                    {
                        Text = element.InnerText ?? "",
                        FontFamily = new FontFamily("Consolas,Monaco,'Courier New',monospace"),
                        Background = Brushes.DarkGray,
                        Padding = new Thickness(4, 2),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(code);
                    break;

                case "pre":
                    var pre = new TextBlock
                    {
                        Text = element.InnerText ?? "",
                        FontFamily = new FontFamily("Consolas,Monaco,'Courier New',monospace"),
                        Background = Brushes.DarkGray,
                        Padding = new Thickness(8),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 4, 0, 4),
                        Foreground = Brushes.White,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        MaxWidth = double.PositiveInfinity
                    };
                    container.Children.Add(pre);
                    break;

                case "br":
                    container.Children.Add(new TextBlock { Height = 8 });
                    break;

                case "div":
                    var divPanel = new StackPanel 
                    { 
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    };
                    ProcessHtmlNode(element, divPanel);
                    container.Children.Add(divPanel);
                    break;

                default:
                    // For unknown elements, just process their children
                    ProcessHtmlNode(element, container);
                    break;
            }
        }

        private void ProcessListItems(HtmlNode listElement, Panel container, string bullet)
        {
            foreach (var item in listElement.Elements("li"))
            {
                var itemPanel = new StackPanel 
                { 
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var bulletText = new TextBlock
                {
                    Text = bullet,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var itemContent = new TextBlock
                {
                    Text = item.InnerText?.Trim() ?? "",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    MaxWidth = double.PositiveInfinity
                };

                itemPanel.Children.Add(bulletText);
                itemPanel.Children.Add(itemContent);
                container.Children.Add(itemPanel);
            }
        }

        private void ProcessOrderedListItems(HtmlNode listElement, Panel container)
        {
            int index = 1;
            foreach (var item in listElement.Elements("li"))
            {
                var itemPanel = new StackPanel 
                { 
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                };

                var numberText = new TextBlock
                {
                    Text = $"{index}. ",
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var itemContent = new TextBlock
                {
                    Text = item.InnerText?.Trim() ?? "",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    MaxWidth = double.PositiveInfinity
                };

                itemPanel.Children.Add(numberText);
                itemPanel.Children.Add(itemContent);
                container.Children.Add(itemPanel);
                index++;
            }
        }

        private double GetHeadingSize(string tagName)
        {
            return tagName.ToLower() switch
            {
                "h1" => 20,
                "h2" => 18,
                "h3" => 16,
                "h4" => 14,
                "h5" => 12,
                "h6" => 11,
                _ => 12
            };
        }

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log error or show message to user
                Console.WriteLine($"Failed to open URL {url}: {ex.Message}");
            }
        }
    }
}
