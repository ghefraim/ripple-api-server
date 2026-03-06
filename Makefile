# EF Core Migration Commands
# Usage:
#   make migration name=MigrationName  - Add a new migration
#   make migrate                       - Apply pending migrations
#   make migrate-script                - Generate SQL script for migrations

PROJECT = src/Application
STARTUP = src/Api
MIGRATIONS_DIR = Infrastructure/Persistence/Migrations

.PHONY: migration migrate migrate-script

migration:
ifndef name
	$(error name is required. Usage: make migration name=MigrationName)
endif
	dotnet ef migrations add "$(name)" --project $(PROJECT) --startup-project $(STARTUP) --output-dir $(MIGRATIONS_DIR)

migrate:
	dotnet ef database update --project $(PROJECT) --startup-project $(STARTUP)

migrate-script:
	dotnet ef migrations script --project $(PROJECT) --startup-project $(STARTUP) --idempotent

remove-migration:
	dotnet ef migrations remove --project $(PROJECT) --startup-project $(STARTUP)

# other dotnet commands

watch:
	cd src/Api && dotnet watch run

build:
	dotnet build	
