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
            StringBuilder fileContents = new StringBuilder();

            string lookupTableFileName = args.Length > (int)Argument.LookupTableFileName
                                            ? args[(int)Argument.LookupTableFileName]
                                            : string.Empty;

            fileContents.AppendLine("Imports System.ComponentModel");
            fileContents.AppendLine();

            fileContents.AppendLine("Public Class Enumerations");
            fileContents.AppendLine();

            string enumsWithoutParents = GetEnumsWithoutParents(lookupTableFileName);
            fileContents.Append(enumsWithoutParents);

            string lookupTableWithParentFileName = args.Length > (int)Argument.LookupTableWithParentFileName
                                            ? args[(int)Argument.LookupTableWithParentFileName]
                                            : string.Empty;

            string enumsWithParents = GetEnumsWithParents(lookupTableWithParentFileName);
            fileContents.AppendLine(enumsWithParents);

            fileContents.AppendLine("End Class");

            WriteToFile(OutputFile.Enumerations, OutputFileExtension.vb, fileContents.ToString());

            Console.WriteLine("Press enter to exit:");
            Console.Read();
        }

        #region Methods

        #region Enums Without Parents

        private static string GetEnumsWithoutParents(string lookupTableFileName)
        {
            StringBuilder fileContents = new StringBuilder();

            List<LookupTable> tables = GetLookupTables(lookupTableFileName);

            List<string> fileLines = GenerateEnumsWithoutParents(tables);

            string enumsWithoutParents = AppendLines(fileLines);
            fileContents.AppendLine(enumsWithoutParents);

            return fileContents.ToString();
        }

        private static List<string> GenerateEnumsWithoutParents(List<LookupTable> tables)
        {
            List<string> fileLines = new List<string>();

            foreach (string schema in tables.Select(t => t.SchemaName).Distinct())
            {
                fileLines.Add($"#Region \"{schema}\"");
                fileLines.Add("");

                foreach (LookupTable table in tables.Where(t => t.SchemaName == schema))
                {
                    fileLines.AddRange(GenerateEnumForLookupTable(table));
                }

                fileLines.Add("#End Region");
                fileLines.Add("");
            }

            return fileLines;
        }

        private static List<LookupTable> GetLookupTables(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{InputFile.LookupTables}.supersecret";
            }

            List<string> lines = GetInputFileLines(fileName);
            const char separator = '.';

            List<LookupTable> tables = lines.Select(line => line.Split(separator).ToList())
                                                .Where(line => line.Count == Enum.GetValues(typeof(LookupTablePart)).Length)
                                                .Select(parts => new LookupTable(parts))
                                                .ToList();

            List<string> errorMessages = GetFileErrors(lines, separator, Enum.GetValues(typeof(LookupTablePart)).Length, "format");

            if (errorMessages.Any())
            {
                Console.WriteLine($"Error: Invalid format in {InputFile.LookupTables} file.");
                WriteToFile(OutputFile.ErrorsInLookupTables, OutputFileExtension.txt, AppendLines(errorMessages));
            }

            return tables;
        }

        private static List<string> GenerateEnumForLookupTable(LookupTable lookupTable)
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

                enumLines.AddRange(lookupValues.Select(value => $"{GetEnumValue(value.Description, value.Value)}"));
            }
            else
            {
                enumLines.Add($"{Tab}{Tab}'TODO: This will not compile because the table is empty.  Fix it!");
            }

            enumLines.Add($"{Tab}End Enum");
            enumLines.Add("");

            return enumLines;
        }

        private static List<LookupValue> GetLookupValues(LookupTable table)
        {
            DataTable dt = GetDataTable($"SELECT {table.TableName}ID, {table.DescriptionColumnName} FROM {table.SchemaName}.{table.TableName}");
            List<LookupValue> lookupValues = dt.Rows.Cast<DataRow>()
                                                .Select(r => new LookupValue(r))
                                                .ToList();

            return lookupValues;
        }

        #endregion

        #region Enums With Parents

        private static string GetEnumsWithParents(string lookupTableFileName)
        {
            StringBuilder fileContents = new StringBuilder();

            List<LookupTableWithParent> tables = GetLookupTablesWithParents(lookupTableFileName);

            if (tables.Any())
            {
                List<string> fileLines = GenerateEnumsWithParents(tables);

                string enumsWithParents = AppendLines(fileLines);
                fileContents.AppendLine(enumsWithParents);
            }

            return fileContents.ToString();
        }

        private static List<string> GenerateEnumsWithParents(List<LookupTableWithParent> tables)
        {
            List<string> fileLines = new List<string>();

            foreach (LookupTableWithParent table in tables)
            {
                fileLines.Add($"{Tab}Public Class {table.SchemaName}_{table.TableName}");
                fileLines.Add("");
                fileLines.AddRange(GenerateEnumsForLookupTableWithParent(table));
                fileLines.Add($"{Tab}End Class");
                fileLines.Add("");
            }

            return fileLines;
        }

        private static List<LookupTableWithParent> GetLookupTablesWithParents(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{InputFile.LookupTablesWithParents}.supersecret";
            }

            List<string> lines = GetInputFileLines(fileName);
            const char separator = ',';

            List<LookupTableWithParent> tables = lines.Select(line => line.Split(separator).ToList())
                                                    .Where(line => line.Count == Enum.GetValues(typeof(LookupTableWithParentPart)).Length)
                                                    .Select(parts => new LookupTableWithParent(parts))
                                                    .ToList();

            List<string> errorMessages = GetFileErrors(lines, separator, Enum.GetValues(typeof(LookupTableWithParentPart)).Length, "format");

            if (errorMessages.Any())
            {
                Console.WriteLine($"Error: Invalid format in {InputFile.LookupTablesWithParents} file.");
                WriteToFile(OutputFile.ErrorsInLookupTablesWithParents, OutputFileExtension.txt, AppendLines(errorMessages));
            }

            return tables;
        }

        private static List<string> GenerateEnumsForLookupTableWithParent(LookupTableWithParent lookupTable)
        {
            List<string> enumLines = new List<string>();

            List<LookupValueWithParent> lookupValues = GetLookupValuesWithParents(lookupTable);

            foreach (string parent in lookupValues.Select(v => v.Parent).Distinct())
            {
                enumLines.Add($"{Tab}{Tab}Public Enum {CleanDescription(parent)}");

                List<string> duplicateDescriptions = lookupValues.Where(v => v.Parent == parent)
                                                                    .GroupBy(v => v.Description)
                                                                    .Where(g => g.Count() > 1)
                                                                    .Select(g => g.Key)
                                                                    .ToList();

                if (duplicateDescriptions.Any())
                {
                    Console.WriteLine("Duplicate descriptions:");
                    Console.WriteLine(duplicateDescriptions);
                }

                enumLines.AddRange(lookupValues.Where(v => v.Parent == parent)
                                                .Select(value => $"{GetEnumValue(value.Description, value.Value, true)}"));

                enumLines.Add($"{Tab}{Tab}End Enum");
                enumLines.Add("");
            }

            return enumLines;
        }

        private static List<LookupValueWithParent> GetLookupValuesWithParents(LookupTableWithParent table)
        {
            DataTable dt = GetDataTable($"SELECT {table.TableName}ID, {table.DescriptionColumnName}, {table.ParentColumnName} FROM {table.SchemaName}.{table.ViewName}");
            List<LookupValueWithParent> lookupValues = dt.Rows.Cast<DataRow>()
                                                        .Select(r => new LookupValueWithParent(r))
                                                        .ToList();

            return lookupValues;
        }

        #endregion

        private static string GetLookupValuePart(DataRow row, LookupValuePart part)
        {
            return row.ItemArray[(int)part].ToString().Trim();
        }

        private static string AppendLines(IEnumerable<string> input)
        {
            return input.Aggregate(new StringBuilder(), (current, next) => current.AppendLine(next)).ToString();
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
                //TODO: Write errors to file
                Console.WriteLine(ex.Message);
            }
            finally
            {
                conn.Close();
            }

            return dt;
        }

        private static string GetEnumValue(string description, int value, bool addExtraTab = false)
        {
            string extraTab = addExtraTab ? Tab : string.Empty;

            return $@"{extraTab}{GetDescriptionAttribute(description)}{
                    extraTab}{Tab}{Tab}{CleanDescription(description)} = {value}";
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

        private static string GetDescriptionAttribute(string description)
        {
            description = description.Replace("\"", "");

            string descriptionAttribute = $"{Tab}{Tab}<Description(\"{description}\")>{Environment.NewLine}";

            return descriptionAttribute;
        }

        private static void WriteToFile(OutputFile fileName, OutputFileExtension fileExtension, string fileContents)
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
            public string SchemaName { get; }
            public string TableName { get; }
            public string DescriptionColumnName { get; }

            public LookupTable(List<string> parts)
            {
                SchemaName = parts[(int)LookupTablePart.SchemaName].Trim();
                TableName = parts[(int)LookupTablePart.TableName].Trim();
                DescriptionColumnName = parts[(int)LookupTablePart.DescriptionColumnName].Trim();
            }
        }

        private class LookupTableWithParent
        {
            public string SchemaName { get; }
            public string TableName { get; }
            public string ViewName { get; }
            public string DescriptionColumnName { get; }
            public string ParentColumnName { get; }

            public LookupTableWithParent(List<string> parts)
            {
                SchemaName = parts[(int)LookupTableWithParentPart.SchemaName].Trim();
                TableName = parts[(int)LookupTableWithParentPart.TableName].Trim();
                ViewName = parts[(int)LookupTableWithParentPart.ViewName].Trim();
                DescriptionColumnName = parts[(int)LookupTableWithParentPart.DescriptionColumnName].Trim();
                ParentColumnName = parts[(int)LookupTableWithParentPart.ParentColumnName].Trim();
            }
        }

        private class LookupValue
        {
            public string Description { get; }
            public int Value { get; }

            public LookupValue(DataRow r)
            {
                Description = GetLookupValuePart(r, LookupValuePart.Description);
                Value = int.Parse(GetLookupValuePart(r, LookupValuePart.ID));
            }
        }

        private class LookupValueWithParent
        {
            public string Parent { get; }
            public string Description { get; }
            public int Value { get; }

            public LookupValueWithParent(DataRow r)
            {
                Parent = GetLookupValuePart(r, LookupValuePart.Parent);
                Description = GetLookupValuePart(r, LookupValuePart.Description);
                Value = int.Parse(GetLookupValuePart(r, LookupValuePart.ID));
            }
        }

        #endregion

        #region Enums

        private enum Argument
        {
            //TODO: SilentModeFlag,
            LookupTableFileName,
            LookupTableWithParentFileName
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

        private enum OutputFile
        {
            Enumerations,
            ErrorsInLookupTables,
            ErrorsInLookupTablesWithParents
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

        private enum LookupTableWithParentPart
        {
            SchemaName,
            TableName,
            ViewName,
            DescriptionColumnName,
            ParentColumnName
        }

        private enum LookupValuePart
        {
            ID,
            Description,
            Parent
        }

        #endregion
    }
}
