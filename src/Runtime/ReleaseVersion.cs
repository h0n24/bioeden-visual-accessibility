using System;
using System.Text.RegularExpressions;
namespace BioEden.NoDOF { internal static class ReleaseVersion {
        // Stable releases outrank prereleases; beta numbers compare numerically.
        internal static bool TryVersion(string value, out Version version)
        {
            version = null;
            var match = Regex.Match(value ?? "", @"^v?(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$");
            if (!match.Success) return false;
            if (!int.TryParse(match.Groups[1].Value, out int a) || !int.TryParse(match.Groups[2].Value, out int b) ||
                !int.TryParse(match.Groups[3].Value, out int c)) return false;
            int beta = int.MaxValue;
            if (match.Groups[4].Success && !int.TryParse(match.Groups[4].Value, out beta)) return false;
            version = new Version(a, b, c, beta);
            return true;
        }

}}

