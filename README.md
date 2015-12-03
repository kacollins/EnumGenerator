# EnumGenerator
Generates enums in vb

Create these text files and put them in a folder called **Inputs** under bin\Debug:
* **LookupTables**.supersecret
    * One enum will be generated per table
    * The primary key must be an integer with the same name as the table + ID (ex: ContactTypeID for the ContactType table)
    * The values in the description column must be unique.
    * Provide these values, separated by periods:
        * SchemaName
        * TableName
        * DescriptionColumnName
    * Ex: Person.ContactType.Name
* **LookupTablesWithParents**.supersecret
    * One class will be generated per table, and one enum will be generated per parent
    * The primary key must be an integer with the same name as the table + ID (ex: ContactTypeID for the ContactType table)
    * The values in the description column can be repeated, but they must be unique for each parent.
    * Create a SQL view joining the lookup table to its parent, returning the following columns:
       * Primary key from lookup table
       * Description from lookup table
       * Parent (description from parent table)
    * Provide these values, separated by commas:
       * SchemaName
       * TableName
       * ViewName
       * DescriptionColumnName
       * ParentColumnName
    * Ex: Person,ContactType,vContactType,Name,CategoryName

The resulting Enumerations.vb file (and file(s) listing any error messages) will go in a folder called **Outputs** under bin\Debug.
