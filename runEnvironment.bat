docker rm -f pgdb-1
docker run -d -p 5432:5432 --name pgdb-1 -e POSTGRES_PASSWORD=%POSTGRES_DOCKER_PASSWORD% postgres
timeout /t 10 /nobreak
database\postgresql\initDb.bat