# EnumGenerator
Generates enums in vb

Create these text files and put them in a folder called **Inputs** under bin\Debug:
* **LookupTables**.supersecret
    * One enum will be generated per table
    * Provide these values, separated by periods:
        * SchemaName
        * TableName
        * DescriptionColumnName
    * Ex: Person.ContactType.Name
* **LookupTablesWithParents**.supersecret
    * One enum will be generated per parent
    * Create a SQL view joining the lookup table to its parent
    * Provide these values, separated by commas:
       * SchemaName
       * TableName
       * ViewName
       * DescriptionColumnName
       * ParentColumnName
    * Ex: Person,ContactType,vContactType,Name,CategoryName

The resulting Enumerations.vb file will go in a folder called **Outputs** under bin\Debug.
