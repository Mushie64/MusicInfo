using System;
using System.IO;
using System.Diagnostics;
using TagLib;
using TagLib.Id3v2;         // .mp3
using TagLib.Ogg;           // .ogg / .flac
using TagLib.Mpeg4;         // .m4a

public struct TrackMetadata
{
    public string hack;
    public string courseID;
    public string courseName;
    public string sourceGame;
    public string sourceTrack;
    public string sourceArtists;   // to string[]?
}

class Program
{
    /// WHAT DO I WANT TO DO?
    /// - INPUT: Sequentially, all SRM-used music files from the folder they are located in.
    /// - Per file, extract the relevant metadata (see below).
    /// - Write the metadata to a new file.
    /// - OUTPUT: .csv file with metadata scraped from all music files, formatted for columns
    /// as defined in https://drive.google.com/file/d/1j33feHBRWGdGTUfThEvZj_KkbhQ1hn1k/view?usp=share_link.
    /// WHAT METADATA DO I ACCESS? - I made a flowchart, look in the link above.
    /// - Hack -> Artist
    /// - Course ID -> TODO/CUSTOM
    /// - Course Name -> TODO/CUSTOM
    /// - Source game/medium -> Title
    /// - Track name -> Album
    /// - Artist(s) -> uhh...
    /// - YTLink -> "" [manually, in sheet]
    /// - YTVolume -> "" [manually, in sheet] 

    /// TODO:
    /// - !! FIX: Comma still separates values in cells. Observe every track that has a comma in the track name (ex. ZAR negative ending).
    ///  (this is with almost 100% certainty a metadata-side issue. (so "blame" the library + do ???)
    ///  (probably hardcode - enforce that we are trying to grab ALL values in Performer(s)/Title(s)/Album(s) and concatenate them.)
    /// - Static mapping/dictionary: when Source Game is or starts with a specific word, write author(s) automatically; ex. Touhou(...) -> ZUN.

    // MUST refer to the current directory with a "./" (respectively ".\")
    const string csvFilename = @".\..\SRM_Metadata.csv";  // Pass a folder with only the music files in it. Create resultant .CSV one folder above.

    const char csvSeparator = '|';

    static string[] allFiles;

    static void Main(string[] args)
    {
        Console.WriteLine("SRM Music Metadata File<->CSV Tool");
        Console.WriteLine("----------------");
        // more welcome/explanatory lines here?

        ProcessUserArgs(args);
    }

    static void ProcessUserArgs(string[] inputs = null)
    {
        if (inputs == null) inputs = Console.ReadLine().Split(' ');

        // TODO: Consistently exit application after 1 input, in order to make ALL calls require typing out the application name.
        // Currently prone to stack overflow (probably)!!

        switch (inputs.Length)
        {
            case 0:
                // Someone just ran the program only, without arguments? Insert explanatory text about what arguments it expects.
                // TODO: more specific information
                Console.WriteLine("Usage: [flag] <music directory path>");
                Console.WriteLine("(or type: --help , to get more information)");

                ProcessUserArgs();
                break;
            case 1:
                // Only one argument? Try checking if it is a path. If yes, default the operation to READ.
                Console.WriteLine($"PATH passed to scrape SRM music files from: {inputs[0]}");
                Console.WriteLine("----------------");

                if (Directory.Exists(inputs[0]))
                {
                    if (ReadFromMetadata(inputs[0]) == false) ProcessUserArgs();
                }
                else
                {
                    Console.WriteLine($"Invalid argument: {inputs[0]}. Either pass only a path, or both a flag and a path.");
                    Console.WriteLine("(Type: --help , to get more information.)");
                    ProcessUserArgs();
                }
                break;
            case 2:
                // Two arguments - assume flag+path in this order (intended usage).
                Console.WriteLine($"PATH passed to scrape SRM music files from: {inputs[1]}");
                Console.WriteLine("----------------");
                ExecuteChosenOperation(inputs[0], inputs[1]);
                break;
            default:
                Console.WriteLine("Invalid argument set. Please read below and try again.");
                Console.WriteLine("----------------");
                Console.WriteLine("Usage: [-r/--read | -w/--write] <music directory path>");
                Console.WriteLine("(or type: --help to get more information)");

                ProcessUserArgs();
                break;
        }
    }

    static string[] GetFilesFromPath(string filePath)
    {
        try
        {
            return Directory.GetFiles(filePath);
        }

        catch (PathTooLongException pathTooLongException)
        {
            Console.Error.WriteLine($"Invalid PATH: {filePath}. {pathTooLongException.Message}");
            return null;
        }
        catch (UnauthorizedAccessException unauthorizedAccessException)
        {
            Console.Error.WriteLine($"Invalid PATH: {filePath}. {unauthorizedAccessException.Message}");
            return null;
        }
        catch (DirectoryNotFoundException directoryNotFoundException)
        {
            Console.Error.WriteLine($"Invalid PATH: {filePath}. {directoryNotFoundException.Message}");
            return null;
        }
        catch (IOException ioExcepton)
        {
            Console.Error.WriteLine($"Invalid PATH: {filePath}. {ioExcepton.Message}");
            return null;
        }
    }

    static void ExecuteChosenOperation(string operationFlag, string argPath)
    {
        switch (operationFlag)
        {
            case "-r":
            case "--read":
                ReadFromMetadata(argPath);
                break;
            case "-w":
            case "--write":
                Console.WriteLine("Writing from CSV to metadata not yet implemented. You can use -r/--read.");
                ProcessUserArgs();
                break;
            case "-h":
            case "--help":
                Console.WriteLine("Usage: [flag] <music directory path>");
                Console.WriteLine("Available [flag]s:");
                Console.WriteLine("\t-r [--read]\tRead music files and output their metadata in a CSV.");
                Console.WriteLine("\t-w [--write]\tWrite metadata from a CSV to music files.");
                Console.WriteLine("\t-h [--help]\tPrint the text you are currently reading.");
                ProcessUserArgs();
                break;
            default:
                Console.WriteLine("Invalid argument set. Please read below and try again.");
                Console.WriteLine("----------------");
                Console.WriteLine("Usage: [-r/--read | -w/--write] <music directory path>");
                Console.WriteLine("(or type: --help to get more information)");
                ProcessUserArgs();
                break;
        }
    }

    static bool ReadFromMetadata(string atFilePath)
    {
        Console.WriteLine("Operation: READ (Files -> .CSV)");
        allFiles = GetFilesFromPath(atFilePath);
        if (allFiles == null)
        {
            Console.WriteLine("READ failed. Try running different arguments.");
            return false;
        }

        Stopwatch programTimer = Stopwatch.StartNew();

        string fullCSVPath = Path.GetFullPath(Path.Combine(atFilePath, csvFilename));
        Console.WriteLine($"CSV output path: {fullCSVPath}");

        // TODO: try/catch OR do not print "read successful" line
        CreateCSVFromMetadata(allFiles, fullCSVPath);

        programTimer.Stop();

        Console.WriteLine("READ: File metadata to .CSV successful.");
        Console.WriteLine("Operation time elapsed: " + programTimer.ElapsedMilliseconds + " ms.");
        Console.WriteLine("Press ENTER to exit.");
        return true;
    }

    static void CreateCSVFromMetadata(string[] fromDirectory, string toCSVFile)
    {
        using (StreamWriter fs = new StreamWriter(toCSVFile))   // ~~TL;DR: using { } handles closing, disposing of etc. the FileStream at end of scope
        {
            foreach (string filename in fromDirectory)
            {
                TagLib.File fileObj = TagLib.File.Create(filename);
                TrackMetadata fileMetadata = new TrackMetadata();

                // TODO: Query all performers/title/albums/artists instead, concatenate the results separated by ',' in entries
                fileMetadata.hack = fileObj.Tag.FirstPerformer;     // old foobar2000 definition
                fileMetadata.sourceGame = fileObj.Tag.Title;        // old foobar2000 definition
                fileMetadata.sourceTrack = fileObj.Tag.Album;       // old foobar2000 definition
                fileMetadata.sourceArtists = "";                    // NONE, unless statically specified (SEE TODO)

                TagLib.Tag fileTags;

                // .mp3 (ID3v2) CUSTOM FIELDS - VIA "TXXX" FRAME
                if ((fileTags = fileObj.GetTag(TagTypes.Id3v2)) != null)
                {
                    // Got it!! (ID3v2)TXXX = (TagLib.Id3v2.)UserTextInformationFrame
                    // Output format is [Frame.Description] Frame.Text, for example: [Course ID] MC
                    // but Frame.Text is a string[] ...
                    foreach (UserTextInformationFrame fTXXX in (fileTags as TagLib.Id3v2.Tag).GetFrames<UserTextInformationFrame>())
                    {
                        // custom metadata fields (TXXX):
                        string frameDesc = fTXXX.Description.ToLower();
                        if (frameDesc == "course id") fileMetadata.courseID = fTXXX.Text[0];
                        else if (frameDesc == "course name") fileMetadata.courseName = fTXXX.Text[0];

                        // More custom fields? TBD.
                    }
                }

                // .ogg / .flac (Xiph) CUSTOM FIELDS - VIA "GetField(CUSTOMFIELD)"
                else if ((fileTags = fileObj.GetTag(TagTypes.Xiph)) != null)
                {
                    string[] courseIDTag;
                    // GetField (or the metadata itself) is Case INsensitive
                    if ((courseIDTag = (fileTags as XiphComment).GetField("Course ID")).Length > 0)
                        fileMetadata.courseID = courseIDTag[0];
                    string[] courseNameTag;
                    if ((courseNameTag = (fileTags as XiphComment).GetField("Course Name")).Length > 0)
                        fileMetadata.courseName = courseNameTag[0];
                }

                // .m4a (AAC? Apple whatever?) CUSTOM FIELDS - VIA "----" (DASH BOXES)
                else if ((fileTags = fileObj.GetTag(TagTypes.Apple)) != null)
                {
                    // ~~no, I don't know why the format for the following method is this. but it works. please don't give me more m4a files ever.
                    fileMetadata.courseID = (fileTags as AppleTag).GetDashBox("com.apple.iTunes", "Course ID");
                    fileMetadata.courseName = (fileTags as AppleTag).GetDashBox("com.apple.iTunes", "Course Name");
                }


                //continue;     // Disable CSV writing (DEBUG only)

                fs.Write($"{Path.GetFileName(fileObj.Name)}");              // special - filename for sorting only??
                fs.Write(csvSeparator);
                fs.Write($"{fileMetadata.hack}");
                fs.Write(csvSeparator);
                fs.Write($"{fileMetadata.courseID}");
                fs.Write(csvSeparator);
                fs.Write($"{fileMetadata.courseName}");
                fs.Write(csvSeparator);
                fs.Write($"{fileMetadata.sourceGame}");
                fs.Write(csvSeparator);
                fs.Write($"{fileMetadata.sourceTrack}");
                fs.Write(csvSeparator);
                fs.Write($"{fileMetadata.sourceArtists}");
                fs.Write(csvSeparator);
                fs.Write($"");                              // YTLink = MANUALLY WRITTEN
                fs.Write(csvSeparator);
                fs.Write($"");                              // YTVolume = MANUALLY WRITTEN
                fs.WriteLine();
            }
        }
    }
}