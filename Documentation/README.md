# Corrected system diagrams and descriptions

These project copies replace the outdated content in the three reference files in `C:\Users\Clark\Downloads`. The originals were left untouched.

- `System_ERD_Corrected.drawio` is the editable database diagram. It is generated from `Database/ynclino_schema.sql`, including all 11 tables and 17 foreign keys.
- `System_ERD_Corrected.pdf` is a printable view of the same entities and references.
- `System_Use_Cases_Corrected.drawio` has seven pages for the implemented modules and role permissions.
- `Use_Case_Descriptions_Corrected.docx` describes the corrected use cases. The editable diagrams are separate files so their labels remain editable.

Run `node Documentation/generate-docs.js` from the project root to regenerate these files after a schema or workflow change. The use case descriptions in that script must be reviewed against the controllers when behavior changes.

## UML class diagram

Open `System_Class_Diagram.html` for three readable, linked views of the C# domain model. `System_Class_Diagram.pdf` is the printable version. Each `Class_Diagram_*.svg` opens at full resolution; the matching `.mmd` file is editable Mermaid source. The diagram uses domain names for readability and shows each actual C# model name inside its class box. Regenerate it with `node Documentation/generate-class-diagrams.js` after model or relationship changes.
