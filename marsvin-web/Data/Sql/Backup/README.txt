MarsvinDb.bak
=============

A full SQL Server backup of MarsvinDb, taken at the point the demo data
was seeded (6 animals, 12 accessories, 18 products total).

You do NOT need this file to run the project - "dotnet run" recreates and
reseeds the database automatically every time (see ../schema.sql and
../../DbInitializer.cs, and
marsvin-web/DocumentationInformation/DATABASE-NOTES.txt). This backup is
here as a point-in-time snapshot you can restore directly, e.g. to inspect
the data without running the app, or to hand the database to someone else
as a single file.

HOW TO RESTORE IT
------------------
In SSMS:
  1. Connect to (localdb)\MSSQLLocalDB.
  2. Right-click Databases -> Restore Database...
  3. Source: Device -> browse to this .bak file.
  4. Destination: give it a database name (e.g. MarsvinDb_Restored, so you
     don't collide with the live MarsvinDb the app manages).
  5. Click OK.

From the command line:
  sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "RESTORE DATABASE MarsvinDb_Restored FROM DISK = N'<full path to MarsvinDb.bak>' WITH MOVE 'MarsvinDb' TO 'C:\Users\<you>\MarsvinDb_Restored.mdf', MOVE 'MarsvinDb_log' TO 'C:\Users\<you>\MarsvinDb_Restored_log.ldf';"

(Adjust the MOVE paths for where you want the restored files to live -
LocalDB has no default data directory of its own.)
