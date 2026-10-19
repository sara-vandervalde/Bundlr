
using System;
using System.CommandLine;
using System.Linq;
using System.Reflection.Metadata;

var languageExtensions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["cs"] = [".cs"],
    ["js"] = [".js"],
    ["ts"] = [".ts"],
    ["py"] = [".py"],
    ["java"] = [".java"],
    ["cpp"] = [".cpp", ".h"],
    ["html"] = [".html"],
    ["css"] = [".css"],
    ["sql"] = [".sql"]
};

var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "Programming languages to include (e.g. cs js py), or 'all'",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};



var outputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "File path and name",
    Required = true,
};

var noteOption = new Option<bool>("--note", "-n")
{
    Description = "Add a comment with the source file name and relative path"
};

var sortOption = new Option<string>("--sort", "-s")
{
    Description = "Sort files by 'name' (default) or 'type'",
    DefaultValueFactory = _ => "name"
};
sortOption.AcceptOnlyFromAmong("name", "type");

var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "-r")
{
    Description = "Remove empty lines from the source code"
};

var authorOption = new Option<string>("--author", "-a")
{
    Description = "Name of the bundle author"
};

var bundleCommand = new Command("bundle", "Bundle files into a single package");

bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(sortOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);


bundleCommand.SetAction(parseResult =>
{
    try
    {
        //read values
        string[] languages = parseResult.GetValue(languageOption)!;
        FileInfo output = parseResult.GetValue(outputOption);
        bool note = parseResult.GetValue(noteOption);
        string sort = parseResult.GetValue(sortOption);
        bool removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
        string? author = parseResult.GetValue(authorOption);

        //חישוב הסיומות
        HashSet<string> extensions;
        if (parseResult.GetValue(languageOption).Contains("all", StringComparer.OrdinalIgnoreCase))
        {
            //אם כתבה all, לוקחים את כל הסיומות מהמילון.
            //הופך את הרשימה ל-HashSet כדי למנוע כפילויות
            extensions = languageExtensions.Values.SelectMany(e => e).ToHashSet();
        }

        else
        {
            //אם התנאי if לא התקיים, כלומר נבחרו שפות ספציפיות כמו cs py, מבצעים את מה שכאן.
            extensions = [];
            foreach (var lang in languages)
            {
                //ם קיים כמפתח במילון , אם כן, הסיומות שלו יוכנסו למשתנהexts  
                if (languageExtensions.TryGetValue(lang, out var exts))
                {
                    foreach (var ext in exts)
                    {
                        extensions.Add(ext);
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Language '{lang}' is not recognized and will be ignored.");
                }
            }
        }

        if (extensions.Count == 0)
        {
            Console.WriteLine("ERROR: No valid languages were selected");
            return;
        }


        //מציאת הקבצים

        //רשימת תיקיות שלא רוצים לחפש בהן
        string[] excludedFolders = ["bin", "obj", "debug", "release", ".git", ".vs", "node_modules"];

        //מה זה? מביא את כל הקבצים בתיקייה הנוכחית ובכל תתי-התיקיות שלה.
        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories)
        //מה זה? משאיר רק קבצים שהסיומת שלהם נמצאת ב-extensions, הרשימה שבנינו בשלב 2.
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                // זורק קבצים שנמצאים בתוך bin, obj וכו'ומתעלם מאותיות גדולות וקטנות
                .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => excludedFolders.Contains(part, StringComparer.OrdinalIgnoreCase)))
                    //מה זה? מוודא שהכלי לא יקרא את קובץ הפלט לתוך עצמו.
                    .Where(f => Path.GetFullPath(f) != output.FullName);


        //יכתוב את התוכן לפי סיומת הקובץובתוך כל סוג ממין לפי שם
        if (sort == "type")
            files = files.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f));
        else
            //ממין לפי שם הקובץ בלבד
            files = files.OrderBy(f => Path.GetFileName(f));


        if (!files.Any())
        {
            Console.WriteLine("ERROR: No matching code files were found in this folder");
            return;
        }

        //Open the bundle file
        using var writer = new StreamWriter(output.FullName);

        //כתיבת שם היוצר , אם ביקש
        if (!string.IsNullOrWhiteSpace(author))
            writer.WriteLine($"// Author: {author}");

        foreach (var file in files)
        {
            if (note)
            {
                string relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), file);
                writer.WriteLine($"// Source: {relativePath}");

            }
            string[] lines = File.ReadAllLines(file);

            //בדיקת שורות ריקות
            if (removeEmptyLines)
                lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

            foreach (var line in lines)
                writer.WriteLine(line);

            writer.WriteLine();
        }
        Console.WriteLine("File was created");
    }
    catch (DirectoryNotFoundException ex)
    {
        Console.WriteLine("ERROR: File path is invalid");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("ERROR: No permission to write to this location");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"ERROR: Could not write the file: {ex.Message}");
    }
});


//create-rsp
var createRspCommand = new Command("create-rsp", "Create a response file with a ready bundle command");
createRspCommand.SetAction(parseResult =>
{
    string? languages;
    bool languagesValid;
    do
    {
        Console.Write("Languages to include (e.g. cs js py, or all): ");
        languages = Console.ReadLine()?.Trim();
        languagesValid = true;

        if (string.IsNullOrWhiteSpace(languages))
        {
            Console.WriteLine("ERROR: Language is required. Please try again.");
            languagesValid = false;
        }
        else
        {
            string[] langList = languages.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var lang in langList)
            {
                if (lang.ToLower() != "all" && !languageExtensions.ContainsKey(lang))
                {
                    Console.WriteLine($"ERROR: Unknown language '{lang}'.");
                    languagesValid = false;
                }
            }
        }
    }
    while (!languagesValid);


    string? output;
    bool outputValid;
    do
    {
        Console.Write("Output file name or full path: ");
        output = Console.ReadLine();
        outputValid = true;
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine("ERROR: Output file is required. Please try again.");
            outputValid = false;
        }
        else
        {
            string? folder = Path.GetDirectoryName(Path.GetFullPath(output));
            if (folder is null || !Directory.Exists(folder))
            {
                Console.WriteLine("ERROR: The folder does not exist. Please try again.");
                outputValid = false;
            }
        }
    }
    while (!outputValid);


    string? noteAnswer;
    do
    {
        Console.Write("Add source file comments? (y/n): ");
        noteAnswer = Console.ReadLine()?.Trim().ToLower();

        if (noteAnswer != "y" && noteAnswer != "n")
            Console.WriteLine("ERROR: Please answer y or n.");
    }
    while (noteAnswer != "y" && noteAnswer != "n");

    bool note = noteAnswer == "y";


    string? removeAnswer;
    do
    {
        Console.Write("Remove empty lines? (y/n): ");
        removeAnswer = Console.ReadLine()?.Trim().ToLower();

        if (removeAnswer != "y" && removeAnswer != "n")
            Console.WriteLine("ERROR: Please answer y or n.");
    }
    while (removeAnswer != "y" && removeAnswer != "n");

    bool removeEmptyLines = removeAnswer == "y";


    string? sort;
    do
    {
        Console.Write("Sort by 'name' or 'type' (press Enter for name): ");
        sort = Console.ReadLine()?.Trim().ToLower();

        if (string.IsNullOrEmpty(sort))
            sort = "name";


        if (sort != "name" && sort != "type")
            Console.WriteLine("ERROR: Please answer name or type.");
    }
    while (sort != "name" && sort != "type");



    Console.Write("Author name (press Enter to skip): ");
    string? author = Console.ReadLine()?.Trim();


    //הרכבת הפקודה מכל התשובות
    string command = "bundle";
    command += $" --language {languages}";//לא עוטפים במרכאות. cs js צריכים להישאר שני ערכים נפרדים,
    command += $" --output \"{output}\"";//עוטפים את הנתיב במרכאות, כי אם יש בו רווח
    if (note)
        command += " --note";

    if (removeEmptyLines)
        command += " --remove-empty-lines";
    command += $" --sort {sort}";
    if (!string.IsNullOrWhiteSpace(author))
        command += $" --author \"{author}\"";


    //לשאול איך לקרוא לקובץ ולשמור אותו
    string? rspName;
    do
    {
        Console.Write("Response file name (e.g. myBundle): ");
        rspName = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(rspName))
            Console.WriteLine("ERROR: File name is required. Please try again.");
    }
    while (string.IsNullOrWhiteSpace(rspName));

    if (!rspName.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase))
        rspName += ".rsp";


    //כתיבה לקובץ
    try
    {
        File.WriteAllText(rspName, command);
        Console.WriteLine($"Response file created: {Path.GetFullPath(rspName)}");
        Console.WriteLine($"Run it with: bundlr  @{rspName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: Could not create the response file: {ex.Message}");
    }

});



var RootCommand = new RootCommand("Root command for File Bundler CLI");
RootCommand.Subcommands.Add(bundleCommand);
RootCommand.Subcommands.Add(createRspCommand);

return RootCommand.Parse(args).Invoke();