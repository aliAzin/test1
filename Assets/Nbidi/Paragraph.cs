using System;
using System.Collections.Generic;
using System.Text;

public class Paragraph
{
    string _original_text;
    string _text;
    string _bidi_text;
    char _paragraph_separator = BidiChars.NotAChar;

    byte embedding_level;
    CharData[] _text_data;
    List<int> _char_lengths;
    List<int> _bidi_indexes;

    bool _hasArabic;
    bool _hasNSMs;

    public Paragraph(string text)
    {
        _char_lengths = new List<int>();
        _bidi_indexes = new List<int>();

        Text = text;
    }

    public string Text
    {
        get { return _original_text; }
        set
        {
            _original_text = value;
            _text = value;

            NormalizeText();

            RecalculateParagraphEmbeddingLevel();
            RecalculateCharactersEmbeddingLevels();

            RemoveBidiMarkers();
        }
    }

    public char ParagraphSeparator
    {
        get { return _paragraph_separator; }
        internal set { _paragraph_separator = value; }
    }

    public string BidiText
    {
        get
        {
            string ret = _bidi_text;
            if (_paragraph_separator != BidiChars.NotAChar)
                ret += _paragraph_separator;
            return ret;
        }
    }

    public List<int> BidiIndexes
    {
        get { return _bidi_indexes; }
    }

    public List<int> BidiIndexLengths
    {
        get { return _char_lengths; }
    }

    public byte EmbeddingLevel
    {
        get { return embedding_level; }
        set { embedding_level = value; }
    }

    private void RemoveBidiMarkers()
    {
        string controlChars = "\u200F\u202B\u202E\u200E\u202A\u202D\u202C";

        StringBuilder sb = new StringBuilder(_bidi_text);

        int i = 0;
        while (i < sb.Length)
        {
            if (controlChars.Contains(sb[i].ToString()))
            {
                sb.Remove(i, 1);
                _bidi_indexes.RemoveAt(i);
                _char_lengths.RemoveAt(i);
            }
            else
                ++i;
        }

        _bidi_text = sb.ToString();
    }


    public void RecalculateParagraphEmbeddingLevel()
    {
        foreach (char c in _text)
        {
            BidiCharacterType cType = UnicodeCharacterDataResolver.GetBidiCharacterType(c);
            if (cType == BidiCharacterType.R || cType == BidiCharacterType.AL)
            {
                embedding_level = 1;
                break;
            }
            else if (cType == BidiCharacterType.L)
                break;
        }
    }

    public void NormalizeText()
    {
        StringBuilder sb = InternalDecompose(_char_lengths);
        NbidiStatics.InternalCompose(sb, _char_lengths);

        _text = sb.ToString();
    }

    public void RecalculateCharactersEmbeddingLevels()
    {
        if (_hasArabic)
            _text = NbidiStatics.PerformArabicShaping(_text,_char_lengths);

        _text_data = new CharData[_text.Length];

        #region rules X1 - X9
        // X1
        byte embeddingLevel = EmbeddingLevel;
        DirectionalOverrideStatus dos = DirectionalOverrideStatus.Neutral;
        Stack<DirectionalOverrideStatus> dosStack = new Stack<DirectionalOverrideStatus>();
        Stack<byte> elStack = new Stack<byte>();
        int idx = 0;
        for (int i = 0; i < _text.Length; ++i)
        {
            bool x9Char = false;
            char c = _text[i];
            _text_data[i]._ct = UnicodeCharacterDataResolver.GetBidiCharacterType(c);
            _text_data[i]._char = c;
            _text_data[i]._idx = idx;
            idx += _char_lengths[i];

            #region rules X2 - X5
            // X2. With each RLE, compute the least greater odd embedding level.
            // X4. With each RLO, compute the least greater odd embedding level.
            if (c == BidiChars.RLE || c == BidiChars.RLO)
            {
                x9Char = true;
                if (embeddingLevel < 60)
                {
                    elStack.Push(embeddingLevel);
                    dosStack.Push(dos);

                    ++embeddingLevel;
                    embeddingLevel |= 1;

                    if (c == BidiChars.RLE)
                        dos = DirectionalOverrideStatus.Neutral;
                    else
                        dos = DirectionalOverrideStatus.RTL;
                }
            }
            // X3. With each LRE, compute the least greater even embedding level.
            // X5. With each LRO, compute the least greater even embedding level.
            else if (c == BidiChars.LRE || c == BidiChars.LRO)
            {
                x9Char = true;
                if (embeddingLevel < 59)
                {
                    elStack.Push(embeddingLevel);
                    dosStack.Push(dos);

                    embeddingLevel |= 1;
                    ++embeddingLevel;

                    if (c == BidiChars.LRE)
                        dos = DirectionalOverrideStatus.Neutral;
                    else
                        dos = DirectionalOverrideStatus.LTR;
                }
            }
            #endregion

            #region rule X6
            // X6. For all types besides RLE, LRE, RLO, LRO, and PDF: (...)
            else if (c != BidiChars.PDF)
            {
                // a. Set the level of the current character to the current embedding level.
                _text_data[i]._el = embeddingLevel;

                //b. Whenever the directional override status is not neutral,
                //reset the current character type to the directional override status.
                if (dos == DirectionalOverrideStatus.LTR)
                    _text_data[i]._ct = BidiCharacterType.L;
                else if (dos == DirectionalOverrideStatus.RTL)
                    _text_data[i]._ct = BidiCharacterType.R;
            }
            #endregion

            #region rule X7
            //Terminating Embeddings and Overrides
            // X7. With each PDF, determine the matching embedding or override code.
            // If there was a valid matching code, restore (pop) the last remembered (pushed)
            // embedding level and directional override.
            else if (c == BidiChars.PDF)
            {
                x9Char = true;
                if (elStack.Count > 0)
                {
                    embeddingLevel = elStack.Pop();
                    dos = dosStack.Pop();
                }
            }
            #endregion

            // X8. All explicit directional embeddings and overrides are completely
            // terminated at the end of each paragraph. Paragraph separators are not
            // included in the embedding.

            if (x9Char || _text_data[i]._ct == BidiCharacterType.BN)
            {
                _text_data[i]._el = embeddingLevel;
            }
        }
        #endregion

        int prevLevel = EmbeddingLevel;
        int start = 0;
        while (start < _text.Length)
        {
            #region rule X10 - run level setup
            byte level = _text_data[start]._el;
            BidiCharacterType sor = NbidiStatics.TypeForLevel(Math.Max(prevLevel, level));

            int limit = start + 1;
            while (limit < _text.Length && _text_data[limit]._el == level)
                ++limit;

            byte nextLevel = limit < _text.Length ? _text_data[limit]._el : EmbeddingLevel;
            BidiCharacterType eor = NbidiStatics.TypeForLevel(Math.Max(nextLevel, level));
            #endregion

            ResolveWeakTypes(start, limit, sor, eor);
            NbidiStatics.ResolveNeutralTypes(start, limit, sor, eor, level,_text_data);
            NbidiStatics.ResolveImplicitTypes(start, limit, level,_text_data);

            prevLevel = level;
            start = limit;
        }

        NbidiStatics.ReorderString(_text_data,EmbeddingLevel);
        FixMirroredCharacters();

        List<int> indexes = new List<int>();
        List<int> lengths = new List<int>();

        StringBuilder sb = new StringBuilder();
        foreach (CharData cd in _text_data)
        {
            sb.Append(cd._char);
            indexes.Add(cd._idx);
            lengths.Add(1);
        }

        _bidi_text = sb.ToString();
        _bidi_indexes = indexes;
    }

    /// <summary>
    /// 3.3.3 Resolving Weak Types
    /// </summary>
    private void ResolveWeakTypes(int start, int limit, BidiCharacterType sor, BidiCharacterType eor)
    {
        // TODO - all these repeating runs seems somewhat unefficient...
        // TODO - rules 2 and 7 are the same, except for minor parameter changes...

        #region rule W1
        // W1. Examine each nonspacing mark (NSM) in the level run, and change the type of the NSM to the type of the previous character. If the NSM is at the start of the level run, it will get the type of sor.
        if (_hasNSMs)
        {
            BidiCharacterType preceedingCharacterType = sor;
            for (int i = start; i < limit; ++i)
            {
                BidiCharacterType t = _text_data[i]._ct;
                if (t == BidiCharacterType.NSM)
                    _text_data[i]._ct = preceedingCharacterType;
                else
                    preceedingCharacterType = t;
            }
        }
        #endregion

        #region rule W2
        // W2. Search backward from each instance of a European number until the first strong type (R, L, AL, or sor) is found. If an AL is found, change the type of the European number to Arabic number.

        BidiCharacterType t_w2 = BidiCharacterType.EN;
        for (int i = start; i < limit; ++i)
        {
            if (_text_data[i]._ct == BidiCharacterType.L || _text_data[i]._ct == BidiCharacterType.R)
                t_w2 = BidiCharacterType.EN;
            else if (_text_data[i]._ct == BidiCharacterType.AL)
                t_w2 = BidiCharacterType.AN;
            else if (_text_data[i]._ct == BidiCharacterType.EN)
                _text_data[i]._ct = t_w2;
        }
        #endregion

        #region rule #3
        // W3. Change all ALs to R.
        if (_hasArabic)
        {
            for (int i = start; i < limit; ++i)
            {
                if (_text_data[i]._ct == BidiCharacterType.AL)
                    _text_data[i]._ct = BidiCharacterType.R;
            }
        }
        #endregion

        #region rule W4
        // W4. A single European separator between two European numbers changes to a European number. A single common separator between two numbers of the same type changes to that type.

        // Since there must be values on both sides for this rule to have an
        // effect, the scan skips the first and last value.
        //
        // Although the scan proceeds left to right, and changes the type values
        // in a way that would appear to affect the computations later in the scan,
        // there is actually no problem.  A change in the current value can only
        // affect the value to its immediate right, and only affect it if it is
        // ES or CS.  But the current value can only change if the value to its
        // right is not ES or CS.  Thus either the current value will not change,
        // or its change will have no effect on the remainder of the analysis.

        for (int i = start + 1; i < limit - 1; ++i)
        {
            if (_text_data[i]._ct == BidiCharacterType.ES || _text_data[i]._ct == BidiCharacterType.CS)
            {
                BidiCharacterType prevSepType = _text_data[i - 1]._ct;
                BidiCharacterType succSepType = _text_data[i + 1]._ct;
                if (prevSepType == BidiCharacterType.EN && succSepType == BidiCharacterType.EN)
                {
                    _text_data[i]._ct = BidiCharacterType.EN;
                }
                else if (_text_data[i]._ct == BidiCharacterType.CS && prevSepType == BidiCharacterType.AN && succSepType == BidiCharacterType.AN)
                {
                    _text_data[i]._ct = BidiCharacterType.AN;
                }
            }
        }
        #endregion

        #region rule W5
        // W5. A sequence of European terminators adjacent to European numbers changes to all European numbers.
        for (int i = start; i < limit; ++i)
        {
            if (_text_data[i]._ct == BidiCharacterType.ET)
            {
                // locate end of sequence
                int runstart = i;
                int runlimit = NbidiStatics.FindRunLimit(runstart, limit, new BidiCharacterType[] { BidiCharacterType.ET }, _text_data);

                // check values at ends of sequence
                BidiCharacterType t = runstart == start ? sor : _text_data[runstart - 1]._ct;

                if (t != BidiCharacterType.EN)
                    t = runlimit == limit ? eor : _text_data[runlimit]._ct;

                if (t == BidiCharacterType.EN)
                    SetTypes(runstart, runlimit, BidiCharacterType.EN,_text_data);

                // continue at end of sequence
                i = runlimit;
            }
        }
        #endregion

        #region rule W6
        // W6. Otherwise, separators and terminators change to Other Neutral.
        for (int i = start; i < limit; ++i)
        {
            BidiCharacterType t = _text_data[i]._ct;
            if (t == BidiCharacterType.ES || t == BidiCharacterType.ET || t == BidiCharacterType.CS)
            {
                _text_data[i]._ct = BidiCharacterType.ON;
            }
        }
        #endregion

        #region rule W7
        // W7. Search backward from each instance of a European number until the first strong type (R, L, or sor) is found.
        //     If an L is found, then change the type of the European number to L.

        BidiCharacterType t_w7 = sor == BidiCharacterType.L ? BidiCharacterType.L : BidiCharacterType.EN;
        for (int i = start; i < limit; ++i)
        {
            if (_text_data[i]._ct == BidiCharacterType.R)
                t_w7 = BidiCharacterType.EN;
            else if (_text_data[i]._ct == BidiCharacterType.L)
                t_w7 = BidiCharacterType.L;
            else if (_text_data[i]._ct == BidiCharacterType.EN)
                _text_data[i]._ct = t_w7;
        }
        #endregion
    }

   


   
   

    private void FixMirroredCharacters()
    {
        for (int i = 0; i < _text_data.Length; ++i)
        {
            if ((_text_data[i]._el & 1) == 1)
                _text_data[i]._char = BidiCharacterMirrorResolver.GetBidiCharacterMirror(_text_data[i]._char);
        }
    }

    
    private void GetRecursiveDecomposition(bool canonical, char ch, StringBuilder builder)
    {
        string decomp = UnicodeCharacterDataResolver.GetUnicodeDecompositionMapping(ch);
        if (decomp != null && !(canonical && UnicodeCharacterDataResolver.GetUnicodeDecompositionType(ch) != UnicodeDecompositionType.None))
        {
            for (int i = 0; i < decomp.Length; ++i)
            {
                GetRecursiveDecomposition(canonical, decomp[i], builder);
            }
        }
        else // if no decomp, append
        {
            builder.Append(ch);
        }
    }

    private StringBuilder InternalDecompose(List<int> char_lengths)
    {
        StringBuilder target = new StringBuilder();
        StringBuilder buffer = new StringBuilder();

        _hasArabic = false;
        _hasNSMs = false;

        for (int i = 0; i < _text.Length; ++i)
        {
            BidiCharacterType ct = UnicodeCharacterDataResolver.GetBidiCharacterType(_text[i]);
            _hasArabic |= ((ct == BidiCharacterType.AL) || (ct == BidiCharacterType.AN));
            _hasNSMs |= (ct == BidiCharacterType.NSM);

            buffer.Length = 0;
            GetRecursiveDecomposition(false, _text[i], buffer);
            char_lengths.Add(1 - buffer.Length);

            char ch;
            for (int j = 0; j < buffer.Length; ++j)
            {
                ch = buffer[j];
                UnicodeCanonicalClass chClass = UnicodeCharacterDataResolver.GetUnicodeCanonicalClass(ch);
                int k = target.Length; // insertion point
                if (chClass != UnicodeCanonicalClass.NR)
                {
                    // bubble-sort combining marks as necessary
                    char ch2;
                    for (; k > 0; --k)
                    {
                        ch2 = target[k - 1];
                        if (UnicodeCharacterDataResolver.GetUnicodeCanonicalClass(ch2) <= chClass) break;
                    }
                }
                target.Insert(k, ch);
            }
        }
        return target;
    }



    public static void SetTypes(int start, int limit, BidiCharacterType newType,CharData[] textData)
    {
        for (int i = start; i < limit; ++i)
        {
            textData[i]._ct = newType;
        }
    }
}