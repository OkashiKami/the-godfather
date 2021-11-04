﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Humanizer;
using Newtonsoft.Json;
using Serilog;
using TheGodfather.Common;
using TheGodfather.Services;

namespace TheGodfather.Modules.Misc.Services
{
    public sealed class RandomService : ITheGodfatherService
    {
        private static ImmutableArray<string> _regularAnswers = new[] {
            "8b-00", "8b-01", "8b-02", "8b-03", "8b-04", "8b-05",
            "8b-06", "8b-07", "8b-08", "8b-09", "8b-10", "8b-11",
            "8b-12", "8b-13",
        }.ToImmutableArray();

        private static ImmutableArray<string> _timeAnswers = new[] {
            "8b-t-00", "8b-t-01", "8b-t-02", "8b-t-03", "8b-t-04", "8b-t-05",
            "8b-t-06", "8b-t-07", "8b-t-08", "8b-t-09", "8b-t-10",
        }.ToImmutableArray();

        private static ImmutableArray<string> _quantityAnswers = new[] {
            "8b-q-00", "8b-q-01", "8b-q-02", "8b-q-03", "8b-q-04",
            "8b-q-05", "8b-q-06", "8b-q-07", "8b-q-08",
        }.ToImmutableArray();

        public bool IsDisabled => false;

        private readonly SecureRandom rng;
        private ImmutableDictionary<char, string> leetAlphabet = new Dictionary<char, string>() {
            { 'a', "4@λ∂" },
            { 'b', "86ß"},
            { 'c', "(<©¢€"},
            { 'd', "Ð∂ð"},
            { 'e', "3€ə£"},
            { 'f', "ʃ"},
            { 'g', "9"},
            { 'h', "#╫"},
            { 'i', "!1|l"},
            { 'j', "]¿ʝ"},
            { 'k', "ɮ"},
            { 'l', "17"},
            { 'm', "m"},
            { 'n', "₪"},
            { 'o', "0¤Ω"},
            { 'p', "q℗þ¶"},
            { 'q', "9¶"},
            { 'r', "®Яʁ"},
            { 's', "5$§š"},
            { 't', "7+†"},
            { 'u', "µ"},
            { 'v', "√"},
            { 'w', "Шɰ"},
            { 'x', "%xЖ×"},
            { 'y', "jЧ¥"},
            { 'z', "ʒ≥"},
        }.ToImmutableDictionary();


        public RandomService(bool loadData = true)
        {
            this.rng = new SecureRandom();
            this.leetAlphabet = new Dictionary<char, string>() {
                { 'i' , "i1" },
                { 'l' , "l1" },
                { 'e' , "e3" },
                { 'a' , "@4" },
                { 't' , "t7" },
                { 'o' , "o0" },
                { 's' , "s5" },
            }.ToImmutableDictionary();
            if (loadData)
                this.TryLoadData("Resources");
        }


        public void TryLoadData(string path)
        {
            string alphabetPath = Path.Combine(path, "leet_alphabet.json");
            try {
                Log.Debug("Loading leet alphabet from {Path}", alphabetPath);
                string json = File.ReadAllText(alphabetPath, Encoding.UTF8);
                Dictionary<char, string>? loadedLeetAlphabet = JsonConvert.DeserializeObject<Dictionary<char, string>>(json);
                if (loadedLeetAlphabet is null)
                    throw new JsonSerializationException();
                this.leetAlphabet = loadedLeetAlphabet.ToImmutableDictionary();
            } catch (Exception e) {
                Log.Error(e, "Failed to load leet alphabet, path: {Path}", alphabetPath);
                throw;
            }
        }

        public bool Coinflip(int trueRatio = 1)
            => this.rng.NextBool(trueRatio);

        public int Dice(int sides = 6)
            => this.rng.Next(sides) + 1;

        public string ToLeet(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text) {
                char code = this.rng.NextBool() ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                if (this.rng.NextBool() && this.leetAlphabet.TryGetValue(code, out string? codes))
                    code = this.rng.ChooseRandomChar(codes);
                sb.Append(code);
            }
            return sb.ToString();
        }

        public string FromLeet(string leet)
        {
            var sb = new StringBuilder();
            foreach (char c in leet.ToLowerInvariant()) {
                KeyValuePair<char, string> match = this.leetAlphabet.FirstOrDefault(kvp => kvp.Value.Contains(c));
                sb.Append(match.Key != '\0' ? match.Key : c);
            }
            return sb.ToString().Humanize(LetterCasing.Sentence);
        }

        public string Size(ulong uid)
            => $"8{new string('=', (int)(uid % 40))}D";

        public string Choice(string optionStr)
        {
            IEnumerable<string> options = optionStr
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct();
            return this.rng.ChooseRandomElement(options);
        }

        public string GetRandomYesNoAnswer()
            => this.rng.ChooseRandomElement(_regularAnswers);

        public string GetRandomTimeAnswer()
            => this.rng.ChooseRandomElement(_timeAnswers);

        public string GetRandomQuantityAnswer()
            => this.rng.ChooseRandomElement(_quantityAnswers);
    }
}
