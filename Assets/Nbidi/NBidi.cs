
using System;
using System.Text;
using System.Collections.Generic;



    public static class NBidi
    {

        public static string LogicalToVisual(string logicalString)
        {
            List<Paragraph> pars = SplitStringToParagraphs(logicalString);
            StringBuilder sb = new StringBuilder();
            foreach (Paragraph p in pars)
                sb.Append(p.BidiText);

            return sb.ToString();
        }


        public static string LogicalToVisual(string logicalString, out List<int> indexes, out List<int> lengths)
        {


            indexes = new List<int>();
            lengths = new List<int>();

            List<Paragraph> pars = SplitStringToParagraphs(logicalString);
            StringBuilder sb = new StringBuilder();
            foreach (Paragraph p in pars)
            {
                sb.Append(p.BidiText);
                indexes.AddRange(p.BidiIndexes);
                lengths.AddRange(p.BidiIndexLengths);
            }

            return sb.ToString();
        }



        public static List<Paragraph> SplitStringToParagraphs(string logicalString)
        {
            List<Paragraph> ret = new List<Paragraph>();
            int i;
            StringBuilder sb = new StringBuilder();
            for (i = 0; i < logicalString.Length; ++i)
            {
                char c = logicalString[i];
                BidiCharacterType cType = UnicodeCharacterDataResolver.GetBidiCharacterType(c);
                if (cType == BidiCharacterType.B)
                {
                    Paragraph p = new Paragraph(sb.ToString());
                    p.ParagraphSeparator = c;
                    ret.Add(p);
                    sb.Length = 0;
                }
                else
                    sb.Append(c);
            }
            if (sb.Length > 0) // string ended without a paragraph separator
            {
                ret.Add(new Paragraph(sb.ToString()));
            }
            return ret;
        }

       
    }

public struct CharData
{
    public char _char;
    public byte _el; // 0-62 => 6
    public BidiCharacterType _ct; // 0-18 => 5
    public int _idx;
}
