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

            string lookupTableWithParentFileName = args.Length > (int)Argument.LookupTableWithParentFileName
                                            ? args[(int)Argument.LookupTableWithParentFileName]
                                            : string.Empty;

            fileContents.AppendLine("Public Class Enumerations");
            fileContents.AppendLine();

            string enumsWithoutParents = GetEnumsWithoutParents(lookupTableFileName);
            fileContents.Append(enumsWithoutParents);

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

            List<LookupTable> tables = lines.Where(line => line.Split(separator).Length == Enum.GetValues(typeof(LookupTablePart)).Length)
                                                .Select(validLine => validLine.Split(separator))
                                                .Select(parts => new LookupTable(parts[(int)LookupTablePart.SchemaName],
                                                                                parts[(int)LookupTablePart.TableName],
                                                                                parts[(int)LookupTablePart.DescriptionColumnName]))
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

        private static List<LookupValue> GetLookupValues(LookupTable table)
        {
            DataTable dt = GetDataTable($"SELECT {table.TableName}ID, {table.DescriptionColumnName} FROM {table.SchemaName}.{table.TableName}");
            List<LookupValue> lookupValues = dt.Rows.Cast<DataRow>()
                                                .Select(r => new LookupValue(int.Parse(r.ItemArray[0].ToString()), r.ItemArray[1].ToString()))
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
                fileLines.Add($"Public Class {table.SchemaName}_{table.TableName}");
                fileLines.Add("");
                fileLines.AddRange(GenerateEnumsForLookupTableWithParent(table));
                fileLines.Add("End Class");
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

            List<LookupTableWithParent> tables = lines.Where(line => line.Split(separator).Length == Enum.GetValues(typeof(LookupTableWithParentPart)).Length)
                                                .Select(validLine => validLine.Split(separator))
                                                .Select(parts => new LookupTableWithParent(parts[(int)LookupTableWithParentPart.SchemaName],
                                                                                            parts[(int)LookupTableWithParentPart.TableName],
                                                                                            parts[(int)LookupTableWithParentPart.ViewName],
                                                                                            parts[(int)LookupTableWithParentPart.DescriptionColumnName],
                                                                                            parts[(int)LookupTableWithParentPart.ParentColumnName]))
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
                enumLines.Add($"{Tab}Public Enum {CleanDescription(parent)}");

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
                                                .Select(value => $"{Tab}{Tab}{CleanDescription(value.Description)} = {value.Value}"));

                enumLines.Add($"{Tab}End Enum");
                enumLines.Add("");
            }

            return enumLines;
        }

        private static List<LookupValueWithParent> GetLookupValuesWithParents(LookupTableWithParent table)
        {
            DataTable dt = GetDataTable($"SELECT {table.TableName}ID, {table.DescriptionColumnName}, {table.ParentColumnName} FROM {table.SchemaName}.{table.ViewName}");
            List<LookupValueWithParent> lookupValues = dt.Rows.Cast<DataRow>()
                                                .Select(r => new LookupValueWithParent(int.Parse(r.ItemArray[0].ToString()), r.ItemArray[1].ToString(), r.ItemArray[2].ToString()))
                                                .ToList();

            return lookupValues;
        }

        #endregion

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

        private class LookupTableWithParent
        {
            public string SchemaName { get; private set; }
            public string TableName { get; private set; }
            public string ViewName { get; private set; }
            public string DescriptionColumnName { get; private set; }
            public string ParentColumnName { get; private set; }

            public LookupTableWithParent(string schemaName, string tableName, string viewName,
                                        string descriptionColumnName, string parentColumnName)
            {
                SchemaName = schemaName;
                TableName = tableName;
                ViewName = viewName;
                DescriptionColumnName = descriptionColumnName;
                ParentColumnName = parentColumnName;
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

        private class LookupValueWithParent
        {
            public string Parent { get; private set; }
            public string Description { get; private set; }
            public int Value { get; private set; }

            public LookupValueWithParent(int value, string description, string parent)
            {
                Value = value;
                Description = description;
                Parent = parent;
            }
        }

        #endregion

        #region Enums

        private enum Argument
        {
            SilentModeFlag,
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

        #endregion
    }
}
