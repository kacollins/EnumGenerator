using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace EnumGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            string errorMessage = "";
            StringBuilder fileContents = new StringBuilder();

            string lookupTableFileName = args.Length > (int)Argument.LookupTableFileName
                                            ? args[(int)Argument.LookupTableFileName]
                                            : string.Empty;

            LookupTableFileResult result = GetLookupTables(lookupTableFileName);

            if (result.Tables.Any())
            {
                List<string> fileLines = GenerateEnumsWithoutParents(result);

                fileContents.AppendLine("Public Class Enumerations");
                fileContents.AppendLine();

                string enumsWithoutParents = AppendLines(fileLines);
                fileContents.AppendLine(enumsWithoutParents);

                fileContents.AppendLine("End Class");
            }
            else
            {
                errorMessage = "No lookup tables found.";
            }

            string enumsWithParents = ""; //TODO: GenerateEnumsWithParents();
            fileContents.AppendLine(enumsWithParents);

            WriteToFile("Enumerations", fileContents.ToString(), OutputFileExtension.vb);

            Console.WriteLine(errorMessage);

            Console.WriteLine("Press enter to exit:");
            Console.Read();
        }

        private static List<string> GenerateEnumsWithoutParents(LookupTableFileResult result)
        {
            List<string> fileLines = new List<string>();

            foreach (string schema in result.Tables.Select(t => t.SchemaName).Distinct())
            {
                fileLines.Add($"#Region \"{schema}\"");
                fileLines.Add("");

                foreach (LookupTable table in result.Tables.Where(t => t.SchemaName == schema))
                {
                    fileLines.AddRange(GenerateEnum(table));
                }

                fileLines.Add("#End Region");
                fileLines.Add("");
            }
            return fileLines;
        }

        #region Methods

        private static string AppendLines(IEnumerable<string> input)
        {
            return input.Aggregate(new StringBuilder(), (current, next) => current.AppendLine(next)).ToString();
        }

        private static List<string> GenerateEnum(LookupTable lookupTable)
        {
            List<string> enumLines = new List<string>();

            List<LookupValue> lookupValues = GetLookupValues(lookupTable);

            string schemaPrefix = lookupTable.SchemaName == "dbo" ? "" : lookupTable.SchemaName + "_";
            enumLines.Add($"{Tab}Public Enum {schemaPrefix}{lookupTable.TableName}");

            if (lookupValues.Any())
            {
                var duplicateDescriptions = lookupValues.GroupBy(v => v.Description)
                                                        .Where(g => g.Count() > 1)
                                                        .Select(g => g.Key)
                                                        .ToList();

                if (duplicateDescriptions.Any())
                {
                    Console.WriteLine("Duplicate descriptions:");
                    Console.WriteLine(duplicateDescriptions);
                }

                enumLines.AddRange(lookupValues.Select(value => $"{Tab}{Tab}{CleanDescription(value.Description)} = {value.Value}"));
            }
            else
            {
                enumLines.Add($"{Tab}{Tab}'TODO: This will not compile because the table is empty.  Fix it!");
            }

            enumLines.Add($"{Tab}End Enum");
            enumLines.Add("");

            return enumLines;
        }

        private static List<string> GetInputFileLines(string fileName)
        {
            List<string> fileLines = new List<string>();

            const char backSlash = '\\';
            DirectoryInfo directoryInfo = new DirectoryInfo($"{CurrentDirectory}{backSlash}{Folder.Inputs}");

            if (directoryInfo.Exists)
            {
                FileInfo file = directoryInfo.GetFiles(fileName).FirstOrDefault();

                if (file == null)
                {
                    Console.WriteLine($"File does not exist: {directoryInfo.FullName}{backSlash}{fileName}");
                }
                else
                {
                    fileLines = File.ReadAllLines(file.FullName)
                                            .Where(line => !string.IsNullOrWhiteSpace(line)
                                                            && !line.StartsWith("--")
                                                            && !line.StartsWith("//")
                                                            && !line.StartsWith("'"))
                                            .ToList();
                }
            }
            else
            {
                Console.WriteLine($"Directory does not exist: {directoryInfo.FullName}");
            }

            return fileLines;
        }

        private static LookupTableFileResult GetLookupTables(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{InputFile.LookupTables}.supersecret";
            }

            List<string> lines = GetInputFileLines(fileName);
            const char separator = '.';

            List<LookupTable> tablesToCompare = lines.Where(line => line.Split(separator).Length == Enum.GetValues(typeof(LookupTablePart)).Length)
                                                .Select(validLine => validLine.Split(separator))
                                                .Select(parts => new LookupTable(parts[(int)LookupTablePart.SchemaName],
                                                                                parts[(int)LookupTablePart.TableName],
                                                                                parts[(int)LookupTablePart.DescriptionColumnName]))
                                                .ToList();

            List<string> errorMessages = GetFileErrors(lines, separator, Enum.GetValues(typeof(LookupTablePart)).Length, "schema/table format");

            if (errorMessages.Any())
            {
                Console.WriteLine($"Error: Invalid schema/table format in {InputFile.LookupTables} file.");
            }

            LookupTableFileResult result = new LookupTableFileResult(tablesToCompare, errorMessages);

            return result;
        }

        private static List<string> GetFileErrors(List<string> fileLines, char separator, int length, string description)
        {
            List<string> errorMessages = fileLines.Where(line => line.Split(separator).Length != length)
                                                    .Select(invalidLine => $"Invalid {description}: {invalidLine}")
                                                    .ToList();

            return errorMessages;
        }

        private static DataTable GetDataTable(string queryText)
        {
            SqlConnection conn = new SqlConnection
            {
                ConnectionString = ConfigurationManager.ConnectionStrings["SQLDBConnection"].ToString()
            };

            SqlDataAdapter sda = new SqlDataAdapter(queryText, conn);
            DataTable dt = new DataTable();

            try
            {
                conn.Open();
                sda.Fill(dt);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        private static List<LookupValue> GetLookupValues(LookupTable table)
        {
            DataTable dt = GetDataTable($"SELECT {table.TableName}ID, {table.DescriptionColumnName} FROM {table.SchemaName}.{table.TableName}");
            List<LookupValue> lookupValues = dt.Rows.Cast<DataRow>()
                                                .Select(r => new LookupValue(int.Parse(r.ItemArray[0].ToString()), r.ItemArray[1].ToString()))
                                                .ToList();

            return lookupValues;
        }
        
        private static string CleanDescription(string description)
        {
            description = description.Replace("&", " and ").Replace("(s)", "s");

            //Replace non-alphanumeric characters with spaces
            description = Regex.Replace(description, "[^0-9a-zA-Z]", " ");

            //Remove extraneous whitespace
            description = Regex.Replace(description, @"\s+", " ");
            description = description.Trim();

            //Replace spaces with underscores
            description = description.Replace(' ', '_');

            //Add underscore to beginning if description starts with a digit or is a reserved word
            if (char.IsDigit(description[0]) || description == "Operator" || description == "Private")
            {
                description = "_" + description;
            }

            return description;
        }
        
        private static void WriteToFile(string fileName, string fileContents, OutputFileExtension fileExtension)
        {
            const char backSlash = '\\';
            string directory = $"{CurrentDirectory}{backSlash}{Folder.Outputs}";

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string filePath = $"{directory}{backSlash}{fileName}.{fileExtension}";

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (StreamWriter sw = File.CreateText(filePath))
            {
                sw.Write(fileContents);
            }

            Console.WriteLine($"Wrote file to {filePath}");
        }
        
        #endregion

        #region Properties

        private static string CurrentDirectory => Directory.GetCurrentDirectory();  //bin\Debug

        private static string Tab => new string(' ', 4);

        #endregion

        #region Classes

        private class LookupTable
        {
            public string SchemaName { get; private set; }
            public string TableName { get; private set; }
            public string DescriptionColumnName { get; private set; }

            public LookupTable(string schemaName, string tableName, string descriptionColumnName)
            {
                SchemaName = schemaName;
                TableName = tableName;
                DescriptionColumnName = descriptionColumnName;
            }
        }

        private class LookupValue
        {
            public string Description { get; private set; }
            public int Value { get; private set; }

            public LookupValue(int value, string description)
            {
                Description = description;
                Value = value;
            }
        }
        
        private class LookupTableFileResult
        {
            public List<LookupTable> Tables { get; }
            public List<string> Errors { get; }

            public LookupTableFileResult(List<LookupTable> tables, List<string> errors)
            {
                Tables = tables;
                Errors = errors;
            }
        }

        #endregion

        #region Enums

        private enum Argument
        {
            SilentModeFlag,
            LookupTableFileName
        }

        private enum Folder
        {
            Inputs,
            Outputs
        }

        private enum InputFile
        {
            LookupTables,
            LookupTablesWithParents
        }

        private enum OutputFileExtension
        {
            vb,
            txt
        }
        
        private enum LookupTablePart
        {
            SchemaName,
            TableName,
            DescriptionColumnName
        }

        #endregion
    }
}
