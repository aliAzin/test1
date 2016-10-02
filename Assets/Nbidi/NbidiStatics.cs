using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


    public class NbidiStatics
    {
    /// <summary>
    /// 3.5 Shaping
    /// Implements rules R1-R7 and rules L1-L3 of section 8.2 (Arabic) of the Unicode standard.
    /// </summary>
    // TODO - this code is very special-cased.
    public static string PerformArabicShaping(string text, List<int> charLengths)
    {
        ArabicShapeJoiningType last_jt = ArabicShapeJoiningType.U;
        LetterForm last_form = LetterForm.Isolated;
        int last_pos = 0;
        char last_char = BidiChars.NotAChar;
        LetterForm[] letterForms = new LetterForm[text.Length];

        for (int curr_pos = 0; curr_pos < text.Length; ++curr_pos)
        {
            char ch = text[curr_pos];
            //string chStr = (ch).ToString("X4");

            ArabicShapeJoiningType jt = UnicodeArabicShapingResolver.GetArabicShapeJoiningType(ch);
            if ((jt == ArabicShapeJoiningType.R ||
                 jt == ArabicShapeJoiningType.D ||
                 jt == ArabicShapeJoiningType.C) &&
                (last_jt == ArabicShapeJoiningType.L ||
                 last_jt == ArabicShapeJoiningType.D ||
                 last_jt == ArabicShapeJoiningType.C))
            {
                if (last_form == LetterForm.Isolated && (last_jt == ArabicShapeJoiningType.D ||
                                                               last_jt == ArabicShapeJoiningType.L))
                {
                    letterForms[last_pos] = LetterForm.Initial;
                }
                else if (last_form == LetterForm.Final && last_jt == ArabicShapeJoiningType.D)
                {
                    letterForms[last_pos] = LetterForm.Medial;
                }
                letterForms[curr_pos] = LetterForm.Final;
                last_form = LetterForm.Final;
                last_jt = jt;
                last_pos = curr_pos;
                last_char = ch;
            }
            else if (jt != ArabicShapeJoiningType.T)
            {
                letterForms[curr_pos] = LetterForm.Isolated;
                last_form = LetterForm.Isolated;
                last_jt = jt;
                last_pos = curr_pos;
                last_char = ch;
            }
            else
                letterForms[curr_pos] = LetterForm.Isolated;
        }

        last_char = BidiChars.NotAChar;
        last_pos = 0;
        int insert_pos = 0;

        StringBuilder sb = new StringBuilder();

        for (int curr_pos = 0; curr_pos < text.Length; ++curr_pos)
        {
            char ch = text[curr_pos];
            //string chStr = (ch).ToString("X4");
            ArabicShapeJoiningType jt = UnicodeArabicShapingResolver.GetArabicShapeJoiningType(ch);

            if (last_char == BidiChars.ARABIC_LAM &&
                ch != BidiChars.ARABIC_ALEF &&
                ch != BidiChars.ARABIC_ALEF_MADDA_ABOVE &&
                ch != BidiChars.ARABIC_ALEF_HAMZA_ABOVE &&
                ch != BidiChars.ARABIC_ALEF_HAMZA_BELOW &&
                jt != ArabicShapeJoiningType.T)
            {
                last_char = BidiChars.NotAChar;
            }
            else if (ch == BidiChars.ARABIC_LAM)
            {
                last_char = ch;
                last_pos = curr_pos;
                insert_pos = sb.Length;
            }

            if (last_char == BidiChars.ARABIC_LAM)
            {
                if (letterForms[last_pos] == LetterForm.Medial)
                {
                    switch (ch)
                    {
                        case BidiChars.ARABIC_ALEF:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_FINAL;
                            charLengths.RemoveAt(insert_pos);
                            continue;

                        case BidiChars.ARABIC_ALEF_MADDA_ABOVE:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_MADDA_ABOVE_FINAL;
                            charLengths.RemoveAt(insert_pos);
                            charLengths[insert_pos] = charLengths[insert_pos] + 1;
                            continue;

                        case BidiChars.ARABIC_ALEF_HAMZA_ABOVE:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_HAMZA_ABOVE_FINAL;
                            charLengths.RemoveAt(insert_pos);
                            continue;

                        case BidiChars.ARABIC_ALEF_HAMZA_BELOW:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_HAMZA_BELOW_FINAL;
                            charLengths.RemoveAt(insert_pos);
                            continue;
                    }
                }
                else if (letterForms[last_pos] == LetterForm.Initial)
                {
                    switch (ch)
                    {
                        case BidiChars.ARABIC_ALEF:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_ISOLATED;
                            charLengths.RemoveAt(insert_pos);
                            continue;

                        case BidiChars.ARABIC_ALEF_MADDA_ABOVE:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_MADDA_ABOVE_ISOLATED;
                            charLengths.RemoveAt(insert_pos);
                            charLengths[insert_pos] = charLengths[insert_pos] + 1;
                            continue;

                        case BidiChars.ARABIC_ALEF_HAMZA_ABOVE:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_HAMZA_ABOVE_ISOLATED;
                            charLengths.RemoveAt(insert_pos);
                            continue;

                        case BidiChars.ARABIC_ALEF_HAMZA_BELOW:
                            sb[insert_pos] = BidiChars.ARABIC_LAM_ALEF_HAMZA_BELOW_ISOLATED;
                            charLengths.RemoveAt(insert_pos);
                            continue;
                    }
                }
            }

            sb.Append(UnicodeArabicShapingResolver.GetArabicCharacterByLetterForm(ch, letterForms[curr_pos]));
        }

        return sb.ToString();
    }


        public static void ResolveImplicitTypes(int start, int limit, int level, CharData[] textData)
        {
            if ((level & 1) == 0) // even level
            {
                for (int i = start; i < limit; ++i)
                {
                    BidiCharacterType t = textData[i]._ct;
                    // Rule I1.
                    if (t == BidiCharacterType.R)
                        textData[i]._el += 1;
                    else if (t == BidiCharacterType.AN || t == BidiCharacterType.EN)
                        textData[i]._el += 2;
                }
            }
            else // odd level
            {
                for (int i = start; i < limit; ++i)
                {
                    BidiCharacterType t = textData[i]._ct;
                    // Rule I2.
                    if (t == BidiCharacterType.L || t == BidiCharacterType.AN || t == BidiCharacterType.EN)
                        textData[i]._el += 1;
                }
            }
        }

        private static char GetPairwiseComposition(char first, char second)
    {
        if (first < 0 || first > 0xFFFF || second < 0 || second > 0xFFFF) return BidiChars.NotAChar;
        return UnicodeCharacterDataResolver.Compose(first.ToString() + second.ToString());
    }

    public static void InternalCompose(StringBuilder target, List<int> char_lengths)
    {
        if (target.Length == 0) return;
        int starterPos = 0;
        int compPos = 1;
        int text_idx = 0;
        char starterCh = target[0];

        char_lengths[starterPos] = char_lengths[starterPos] + 1;

        UnicodeCanonicalClass lastClass = UnicodeCharacterDataResolver.GetUnicodeCanonicalClass(starterCh);

        if (lastClass != UnicodeCanonicalClass.NR)
            lastClass = (UnicodeCanonicalClass)256; // fix for strings staring with a combining mark

        int oldLen = target.Length;

        char ch;
        for (int decompPos = compPos; decompPos < target.Length; ++decompPos)
        {
            ch = target[decompPos];
            UnicodeCanonicalClass chClass = UnicodeCharacterDataResolver.GetUnicodeCanonicalClass(ch);
            char composite = GetPairwiseComposition(starterCh, ch);
            UnicodeDecompositionType composeType = UnicodeCharacterDataResolver.GetUnicodeDecompositionType(composite);

            if (composeType == UnicodeDecompositionType.None &&
                composite != BidiChars.NotAChar &&
                (lastClass < chClass || lastClass == UnicodeCanonicalClass.NR))
            {
                target[starterPos] = composite;
                char_lengths[starterPos] = char_lengths[starterPos] + 1;
                starterCh = composite;
            }
            else
            {
                if (chClass == UnicodeCanonicalClass.NR)
                {
                    starterPos = compPos;
                    starterCh = ch;
                    text_idx++;
                }
                lastClass = chClass;
                target[compPos] = ch;
                int chkPos = compPos;
                if (char_lengths[chkPos] < 0)
                {
                    while (char_lengths[chkPos] < 0)
                    {
                        char_lengths[chkPos] = char_lengths[chkPos] + 1;
                        char_lengths.Insert(compPos, 0);
                        chkPos++;
                    }
                }
                else
                    char_lengths[chkPos] = char_lengths[chkPos] + 1;

                if (target.Length != oldLen) // MAY HAVE TO ADJUST!
                {
                    decompPos += target.Length - oldLen;
                    oldLen = target.Length;
                }
                ++compPos;
            }
        }
        target.Length = compPos;
        char_lengths.RemoveRange(compPos, char_lengths.Count - compPos);
    }

        public  static void ReorderString(CharData[] textData, byte embeddingLevel)
        {

            int l1_start = 0;
            for (int i = 0; i < textData.Length; ++i)
            {
                if (textData[i]._ct == BidiCharacterType.S || textData[i]._ct == BidiCharacterType.B)
                {
                    for (int j = l1_start; j <= i; ++j)
                        textData[j]._el = embeddingLevel;
                }

                if (textData[i]._ct != BidiCharacterType.WS)
                {
                    l1_start = i + 1;
                }
            }
            for (int j = l1_start; j < textData.Length; ++j)
                textData[j]._el = embeddingLevel;

            byte highest = 0;
            byte lowest_odd = 63;
            foreach (CharData cd in textData)
            {
                if (cd._el > highest) highest = cd._el;
                if ((cd._el & 1) == 1 && cd._el < lowest_odd) lowest_odd = cd._el;
            }

            for (byte el = highest; el >= lowest_odd; --el)
            {
                for (int i = 0; i < textData.Length; ++i)
                {
                    if (textData[i]._el >= el)
                    {
                        // find range of text at or above this level
                        int l2_start = i;
                        int limit = i + 1;
                        while (limit < textData.Length && textData[limit]._el >= el)
                        {
                            ++limit;
                        }

                        // reverse run
                        for (int j = l2_start, k = limit - 1; j < k; ++j, --k)
                        {
                            CharData temp_cd = textData[j];
                            textData[j] = textData[k];
                            textData[k] = temp_cd;
                        }

                        // skip to end of level run
                        i = limit;
                    }
                }
            }


        }

        public static BidiCharacterType TypeForLevel(int level)
        {
            return ((level & 1) == 0) ? BidiCharacterType.L : BidiCharacterType.R;
        }


        public static int FindRunLimit(int index, int limit, BidiCharacterType[] validSet, CharData[] textData)
        {
            --index;
            bool found = false;
            while (++index < limit)
            {
                BidiCharacterType t = textData[index]._ct;
                found = false;
                for (int i = 0; i < validSet.Length && !found; ++i)
                {
                    if (t == validSet[i])
                        found = true;
                }

                if (!found)
                    return index; // didn't find a match in validSet
            }
            return limit;
        }

    public static void SetTypes(int start, int limit, BidiCharacterType newType, CharData[] textData)
    {
        for (int i = start; i < limit; ++i)
        {
            textData[i]._ct = newType;
        }
    }


    public static void ResolveNeutralTypes(int start, int limit, BidiCharacterType sor, BidiCharacterType eor, int level, CharData[] textData)
    {


        for (int i = start; i < limit; ++i)
        {
            BidiCharacterType t = textData[i]._ct;
            if (t == BidiCharacterType.WS || t == BidiCharacterType.ON || t == BidiCharacterType.B || t == BidiCharacterType.S)
            {
                // find bounds of run of neutrals
                int runstart = i;
                int runlimit = NbidiStatics.FindRunLimit(runstart, limit, new BidiCharacterType[] { BidiCharacterType.B, BidiCharacterType.S, BidiCharacterType.WS, BidiCharacterType.ON }, textData);

                // determine effective types at ends of run
                BidiCharacterType leadingType;
                BidiCharacterType trailingType;

                if (runstart == start)
                    leadingType = sor;
                else
                {
                    leadingType = textData[runstart - 1]._ct;
                    if (leadingType == BidiCharacterType.AN || leadingType == BidiCharacterType.EN)
                        leadingType = BidiCharacterType.R;
                }

                if (runlimit == limit)
                    trailingType = eor;
                else
                {
                    trailingType = textData[runlimit]._ct;
                    if (trailingType == BidiCharacterType.AN || trailingType == BidiCharacterType.EN)
                        trailingType = BidiCharacterType.R;
                }

                BidiCharacterType resolvedType;
                if (leadingType == trailingType)
                {
                    // Rule N1.
                    resolvedType = leadingType;
                }
                else
                {

                    resolvedType = NbidiStatics.TypeForLevel(level);
                }

                SetTypes(runstart, runlimit, resolvedType, textData);

                i = runlimit;
            }
        }
    }

}

