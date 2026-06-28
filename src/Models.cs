using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FileDentify
{    internal sealed class FileReport
    {
        public string DisplayName;
        public string FullText;
        public readonly List<ReportSection> Sections = new List<ReportSection>();
    }

    internal sealed class ReportSection
    {
        public string Title;
        public readonly List<ReportItem> Items = new List<ReportItem>();

        public string DetailText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Title);
            foreach (var item in Items)
            {
                if (string.Equals((item.Title ?? string.Empty).Trim(), (item.Detail ?? string.Empty).Trim(), StringComparison.Ordinal))
                    sb.AppendLine(item.Title);
                else
                    sb.AppendLine(item.Title + ": " + item.Detail.Replace("\r", "").Replace("\n", " | "));
            }
            return sb.ToString().TrimEnd() + Environment.NewLine + Environment.NewLine;
        }
    }

    internal sealed class ReportItem
    {
        public string Title;
        public string Detail;
    }

}

