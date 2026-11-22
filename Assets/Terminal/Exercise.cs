using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

[Serializable]
public class ExerciseBank
{
    public Exercise[] questions;
}

[Serializable]
public class Exercise
{
    public int id;
    public string prompt;
    public string cwd;
    public string[] steps;

    public string Example(int index)
    {
        if (steps[index] == "{text}") return "연습 내용 " + index;
        if (steps[index] == "{end}") return "Ctrl+C";
        return steps[index].Split('|')[0];
    }

    public bool Accepts(int index, string line, string directory, string previous)
    {
        string rule = steps[index];
        if (rule == "{text}") return line.Length > 0 && !IsEnd(line);
        if (rule == "{end}") return IsEnd(line);
        if (id == 33 && index > 0) return line == rule;
        string answer = Normalize(line, directory);
        if ((id == 67 || id == 68) && index > 0 && answer == previous) return false;
        if (id == 65)
            answer = Regex.Replace(answer, @"^(sudo shutdown (?:-[hP] )?\+5) .+$", "$1");
        return rule.Split('|').Any(option => Normalize(option, directory) == answer);
    }

    public static bool IsEnd(string line)
    {
        return Regex.IsMatch(line.Trim(), @"^(?:ctrl\s*\+\s*[cd]|\^[cd])$", RegexOptions.IgnoreCase);
    }

    public static string Normalize(string line, string directory)
    {
        line = Regex.Replace(line.Trim(), @"\s*(>>?)\s*", " $1 ");
        var words = Regex.Split(line.Trim(), @"\s+").ToList();
        int commandIndex = words[0] == "sudo" && words.Count > 1 ? 1 : 0;
        string command = words[commandIndex];
        if (command == "apt-get") words[commandIndex] = "apt";
        bool fileCommand = Array.IndexOf(new[] { "mkdir","rmdir","touch","mv","cp","rm","cd","cat","ls","less","gzip","zip" }, command) >= 0;
        for (int i = commandIndex + 1; i < words.Count; i++)
        {
            string token = words[i];
            if (token.Length > 1 && (token[0] == '\'' || token[0] == '"') && token[0] == token[token.Length-1] && !token.Substring(1).StartsWith("~"))
                token = token.Substring(1, token.Length-2);
            bool finalPath = i == words.Count - 1 && (command == "chmod" || command == "chown" || command == "chgrp" || command == "head" || command == "tail");
            bool searchRoot = i == commandIndex + 1 && command == "find";
            if ((fileCommand || finalPath || searchRoot) && !token.StartsWith("-") && token != ">" && token != ">>")
                token = ResolvePath(directory, token);
            if (command == "tar" && i == commandIndex + 1 && Regex.IsMatch(token, @"^-?[ctxzpv]+f$"))
                token = "-" + new string(token.TrimStart('-').Replace("v", "").OrderBy(c => c).ToArray());
            words[i] = token;
        }
        if (command == "mkdir" || command == "touch")
            words.Sort(commandIndex + 1, words.Count - commandIndex - 1, StringComparer.Ordinal);
        if (command == "ls" || command == "rm" || command == "usermod")
        {
            int first = commandIndex + 1, end = first;
            while (end < words.Count && Regex.IsMatch(words[end], @"^-[a-zA-Z1]+$")) end++;
            if (end > first)
            {
                string flags = string.Concat(words.GetRange(first,end-first)).Replace("-", "");
                words.RemoveRange(first,end-first);
                words.Insert(first,"-" + new string(flags.Distinct().OrderBy(c => c).ToArray()));
            }
        }
        return string.Join(" ", words);
    }

    public static string ResolvePath(string directory, string value)
    {
        const string home = "/home/student";
        if (value == "~" || value.StartsWith("~/")) value = home + value.Substring(1);
        if (!value.StartsWith("/")) value = directory.Replace("~",home) + "/" + value;
        var segments = new List<string>();
        foreach (var segment in value.Split('/'))
        {
            if (segment == "" || segment == ".") continue;
            if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count-1); }
            else segments.Add(segment);
        }
        return "/" + string.Join("/",segments);
    }
}
