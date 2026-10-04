using System;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Mathematics;
namespace LightTranslate {
    // Markdig invokes inline parsers only in textual blocks, never inside fenced/indented/inline code.
    internal sealed class DelimitedMathParser:InlineParser {
        internal DelimitedMathParser() { OpeningCharacters=new[]{'\\'}; }
        public override bool Match(InlineProcessor processor,ref StringSlice slice) {
            char next=slice.PeekChar(1); if(next!='('&&next!='[') return false;
            int start=slice.Start; string close=next=='['?@"\]":@"\)";
            int end=slice.Text.IndexOf(close,start+2,StringComparison.Ordinal);
            int line,column; int source=processor.GetSourcePosition(start,out line,out column);
            if(end<0||end+1>slice.End) {
                processor.Inline=new LiteralInline { Content=new StringSlice(slice.Text,start,slice.End),Span=new SourceSpan(source,source+slice.End-start),Line=line,Column=column };
                slice.Start=slice.End+1; return true;
            }
            processor.Inline=new MathInline { Delimiter='\\',DelimiterCount=next=='['?2:1,Content=new StringSlice(slice.Text,start+2,end-1),Span=new SourceSpan(source,processor.GetSourcePosition(end+1)),Line=line,Column=column };
            slice.Start=end+2; return true;
        }
    }
}
