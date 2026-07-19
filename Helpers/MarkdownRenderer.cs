using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Panel = System.Windows.Controls.Panel;

namespace Throughput.Helpers;

/// <summary>
/// Renders a small subset of GitHub-flavoured markdown (headings, bold, inline
/// code, links, bullet/numbered lists, horizontal rules) into WPF text blocks.
/// Purpose-built for release notes - not a general markdown engine.
/// </summary>
internal static class MarkdownRenderer
{
    private static readonly Brush Body = Freeze(0xCD, 0xD3, 0xDF);
    private static readonly Brush Heading = Freeze(0xF3, 0xF6, 0xFC);
    private static readonly Brush CodeFg = Freeze(0x9C, 0xDC, 0xFE);
    private static readonly Brush CodeBg = Freeze(0x2A, 0x30, 0x3E);
    private static readonly Brush Accent = Freeze(0x5C, 0x9B, 0xFF);
    private static readonly Brush Rule = Freeze(0x33, 0x3B, 0x4A);
    private static readonly FontFamily Mono = new("Consolas");

    private static readonly Regex InlineRx = new(
        @"(\*\*(?<b>.+?)\*\*)|(`(?<c>.+?)`)|(\[(?<lt>[^\]]+?)\]\((?<lu>[^)]+?)\))|(?<url>https?://[^\s)]+)",
        RegexOptions.Compiled);

    private static readonly Regex NumberedRx = new(@"^\s*\d+\.\s+(?<t>.*)$", RegexOptions.Compiled);

    /// <summary>Parses <paramref name="markdown"/> and fills <paramref name="target"/>.</summary>
    public static void Render(string markdown, Panel target)
    {
        target.Children.Clear();
        if (string.IsNullOrWhiteSpace(markdown)) return;

        var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        foreach (var raw in lines)
        {
            string line = raw.TrimEnd();

            if (line.Length == 0) { target.Children.Add(new Border { Height = 5 }); continue; }

            string t = line.Trim();
            if (t is "---" or "***" or "___")
            {
                target.Children.Add(new Border
                {
                    Height = 1,
                    Background = Rule,
                    Margin = new Thickness(0, 6, 0, 9)
                });
                continue;
            }

            if (line.StartsWith("### ")) { target.Children.Add(HeadingBlock(line[4..], 12.5, 8, 3)); continue; }
            if (line.StartsWith("## ")) { target.Children.Add(HeadingBlock(line[3..], 14.5, 12, 5)); continue; }
            if (line.StartsWith("# ")) { target.Children.Add(HeadingBlock(line[2..], 16, 12, 6)); continue; }

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                target.Children.Add(ListItem("•", trimmed[2..]));
                continue;
            }

            var num = NumberedRx.Match(line);
            if (num.Success)
            {
                target.Children.Add(ListItem("›", num.Groups["t"].Value));
                continue;
            }

            target.Children.Add(Paragraph(line));
        }
    }

    private static TextBlock HeadingBlock(string text, double size, double top, double bottom)
    {
        var tb = new TextBlock
        {
            FontSize = size,
            FontWeight = FontWeights.Bold,
            Foreground = Heading,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, top, 0, bottom)
        };
        AddInlines(tb.Inlines, text);
        return tb;
    }

    private static TextBlock Paragraph(string text)
    {
        var tb = new TextBlock
        {
            FontSize = 12.5,
            Foreground = Body,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Margin = new Thickness(0, 0, 0, 7)
        };
        AddInlines(tb.Inlines, text);
        return tb;
    }

    private static FrameworkElement ListItem(string marker, string text)
    {
        var grid = new Grid { Margin = new Thickness(4, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new TextBlock
        {
            Text = marker,
            Foreground = Accent,
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(dot, 0);

        var tb = new TextBlock
        {
            FontSize = 12.5,
            Foreground = Body,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19
        };
        AddInlines(tb.Inlines, text);
        Grid.SetColumn(tb, 1);

        grid.Children.Add(dot);
        grid.Children.Add(tb);
        return grid;
    }

    private static void AddInlines(InlineCollection target, string text)
    {
        int idx = 0;
        foreach (Match m in InlineRx.Matches(text))
        {
            if (m.Index > idx) target.Add(new Run(text[idx..m.Index]));

            if (m.Groups["b"].Success)
            {
                target.Add(new Run(m.Groups["b"].Value) { FontWeight = FontWeights.Bold, Foreground = Heading });
            }
            else if (m.Groups["c"].Success)
            {
                target.Add(new Run(" " + m.Groups["c"].Value + " ")
                {
                    FontFamily = Mono,
                    Foreground = CodeFg,
                    Background = CodeBg
                });
            }
            else if (m.Groups["lt"].Success)
            {
                target.Add(MakeLink(m.Groups["lt"].Value, m.Groups["lu"].Value));
            }
            else if (m.Groups["url"].Success)
            {
                target.Add(MakeLink(m.Groups["url"].Value, m.Groups["url"].Value));
            }

            idx = m.Index + m.Length;
        }

        if (idx < text.Length) target.Add(new Run(text[idx..]));
    }

    private static Inline MakeLink(string label, string url)
    {
        var link = new Hyperlink(new Run(label))
        {
            Foreground = Accent,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = url
        };
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            link.NavigateUri = uri;
            link.RequestNavigate += (_, e) => OpenUrl(e.Uri.ToString());
        }
        return link;
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { }
    }

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
