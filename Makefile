# CloudScale FleetOps — common tasks.
# Everything here runs locally and costs nothing.

.DEFAULT_GOAL := help
SHELL := /bin/bash

.PHONY: help
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-22s\033[0m %s\n", $$1, $$2}'

# ---- Local stack ----------------------------------------------------------
.PHONY: dot-clean
dot-clean: ## Remove macOS AppleDouble sidecar files (._*)
	@# This repo lives on a non-APFS volume, so macOS writes ._Foo sidecars for
	@# extended attributes. buildx cannot xattr them and aborts the build, and the
	@# C# compiler rejects them as binary input. Scrub before any context read.
	@dot_clean -m . 2>/dev/null || true
	@find . -name '._*' -not -path './frontend/node_modules/*' -delete 2>/dev/null || true

.PHONY: up
up: dot-clean ## Start Postgres, LocalStack and the API (docker compose)
	@test -f .env || (echo "Create .env first: cp .env.example .env" && exit 1)
	docker compose up --build -d
	@echo "API      http://localhost:5080"
	@echo "Swagger  http://localhost:5080/swagger"

.PHONY: down
down: ## Stop the local stack
	docker compose down

.PHONY: clean
clean: ## Stop the local stack and delete its volumes
	docker compose down -v

.PHONY: logs
logs: ## Tail the API logs
	docker compose logs -f api

.PHONY: fmt
fmt: dot-clean ## Format the Terraform (runs in Docker; no local terraform needed)
	@# CI runs `terraform fmt -check`, and its alignment rules are not guessable:
	@# a value that opens a multi-line expression is excluded from the surrounding
	@# alignment group, and a comment between attributes breaks the group too.
	@# Always run the real formatter rather than hand-aligning.
	docker run --rm -v "$(CURDIR)/infra/terraform:/work" -w /work \
		hashicorp/terraform:1.9 fmt -recursive

# ---- Backend --------------------------------------------------------------
.PHONY: build
build: ## Build the .NET solution
	dotnet build backend/FleetOps.sln --configuration Release

.PHONY: test
# Depends on `build` deliberately. `dotnet test` on a solution only compiles the test
# project and its transitive dependencies — nothing references the Lambda, so a broken
# ManifestProcessor would pass `make test` and only fail in CI.
test: build ## Build every project, then run the backend test suite
	dotnet test backend/FleetOps.sln --configuration Release --no-build

.PHONY: migration
migration: ## Scaffold an EF Core migration (make migration NAME=AddSomething)
	./scripts/create-migration.sh $(or $(NAME),InitialCreate)

# ---- Frontend -------------------------------------------------------------
.PHONY: ui
ui: ## Run the Angular dev server on :4200
	cd frontend && npm start

.PHONY: ui-install
ui-install: ## Install frontend dependencies
	cd frontend && npm ci

.PHONY: ui-build
ui-build: ## Production build of the frontend
	cd frontend && npm run build:prod

# ---- Infrastructure -------------------------------------------------------
.PHONY: lambda
lambda: ## Build the Lambda deployment zip
	./scripts/build-lambda.sh

.PHONY: tf-plan
tf-plan: lambda ## Terraform plan against LocalStack
	cd infra/terraform && terraform init -upgrade && terraform plan

.PHONY: tf-apply
tf-apply: lambda ## Terraform apply against LocalStack (free — no real AWS)
	cd infra/terraform && terraform init -upgrade && terraform apply -auto-approve

.PHONY: tf-destroy
tf-destroy: ## Tear down the LocalStack resources
	cd infra/terraform && terraform destroy -auto-approve

.PHONY: manifest-demo
manifest-demo: ## Drop the sample manifest into LocalStack and trigger the Lambda
	aws --endpoint-url=http://localhost:4566 s3 cp \
		infra/terraform/sample-manifest.csv \
		s3://fleetops-manifests-upload-dev/incoming/9074729/V-2026-014.csv
	@echo "Watch the Lambda: aws --endpoint-url=http://localhost:4566 logs tail /aws/lambda/fleetops-dev-manifest-processor --follow"

# ---- Kubernetes -----------------------------------------------------------
.PHONY: k8s-up
k8s-up: dot-clean ## Create the k3d cluster and deploy FleetOps
	./scripts/k3d-up.sh

.PHONY: k8s-down
k8s-down: ## Delete the k3d cluster
	k3d cluster delete fleetops
