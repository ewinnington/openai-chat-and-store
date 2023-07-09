for /f "tokens=*" %%a in (database\postgresql\createDatabase.sql) do (
  echo Running command with parameter: %%a
  docker exec -it pgdb-1 psql -U postgres -c "%%a"
)
for /f "tokens=*" %%a in (database\postgresql\initTables.sql) do (
  echo Running command with parameter: %%a
  docker exec -it pgdb-1 psql -U chatstore -d chatstore -c "%%a"
)