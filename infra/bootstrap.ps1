#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Bootstrap the Azure Blob Storage backend for Terraform remote state.

.DESCRIPTION
    Creates a resource group, storage account, and blob container for Terraform state,
    then writes backend.conf to infra/terraform/. Safe to run multiple times — all
    az commands use --only-show-errors and --query to suppress noise.

    This script must be run once before `terraform init`. It does NOT use Terraform
    because the state backend must exist before Terraform initializes.

.PARAMETER ResourceGroupName
    Resource group for the state storage. Default: rg-tf-state

.PARAMETER StorageAccountName
    Storage account name (globally unique, 3-24 lowercase alphanumeric). Default: stmcpworkshoptfstate

.PARAMETER ContainerName
    Blob container name. Default: tfstate

.PARAMETER StateKey
    Name of the state blob. Default: mcp-server.tfstate

.PARAMETER Location
    Azure region. Default: eastus2

.EXAMPLE
    ./bootstrap.ps1

.EXAMPLE
    ./bootstrap.ps1 -StorageAccountName mystatestorage -Location eastus2
#>
[CmdletBinding()]
param(
    [string] $ResourceGroupName  = "rg-tf-state",
    [string] $StorageAccountName = "stmcpworkshoptfstate",
    [string] $ContainerName      = "tfstate",
    [string] $StateKey           = "mcp-server.tfstate",
    [string] $Location           = "eastus2"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendConfPath = Join-Path $ScriptDir "terraform" "backend.conf"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Write-Step([string] $Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-AzLogin {
    $account = az account show --query "id" -o tsv 2>$null
    if (-not $account) {
        Write-Error "Not logged in to Azure CLI. Run 'az login' first."
        exit 1
    }
    $subscriptionName = az account show --query "name" -o tsv
    Write-Host "    Active subscription: $subscriptionName ($account)" -ForegroundColor Gray
}

# ---------------------------------------------------------------------------
# Pre-flight checks
# ---------------------------------------------------------------------------
Write-Step "Checking Azure CLI login"
Assert-AzLogin

# Validate storage account name (Azure rule: 3-24 chars, lowercase alphanumeric only)
if ($StorageAccountName -notmatch "^[a-z0-9]{3,24}$") {
    Write-Error "Storage account name '$StorageAccountName' is invalid. Must be 3-24 lowercase alphanumeric characters."
    exit 1
}

# ---------------------------------------------------------------------------
# Resource group
# ---------------------------------------------------------------------------
Write-Step "Resource group: $ResourceGroupName"
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq "true") {
    Write-Host "    Already exists — skipping." -ForegroundColor Gray
} else {
    az group create --name $ResourceGroupName --location $Location --only-show-errors | Out-Null
    Write-Host "    Created." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Storage account
# ---------------------------------------------------------------------------
Write-Step "Storage account: $StorageAccountName"
$saExists = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName --query "id" -o tsv 2>$null
if ($saExists) {
    Write-Host "    Already exists — skipping." -ForegroundColor Gray
} else {
    az storage account create `
        --name $StorageAccountName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --allow-blob-public-access false `
        --min-tls-version TLS1_2 `
        --only-show-errors | Out-Null
    Write-Host "    Created." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Blob container
# ---------------------------------------------------------------------------
Write-Step "Blob container: $ContainerName"
$containerExists = az storage container exists `
    --name $ContainerName `
    --account-name $StorageAccountName `
    --auth-mode login `
    --query "exists" -o tsv 2>$null

if ($containerExists -eq "true") {
    Write-Host "    Already exists — skipping." -ForegroundColor Gray
} else {
    az storage container create `
        --name $ContainerName `
        --account-name $StorageAccountName `
        --auth-mode login `
        --only-show-errors | Out-Null
    Write-Host "    Created." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Write backend.conf
# ---------------------------------------------------------------------------
Write-Step "Writing backend.conf"

$subscriptionId = az account show --query "id" -o tsv

$backendConf = @"
subscription_id      = "$subscriptionId"
resource_group_name  = "$ResourceGroupName"
storage_account_name = "$StorageAccountName"
container_name       = "$ContainerName"
key                  = "$StateKey"
"@

Set-Content -Path $BackendConfPath -Value $backendConf -Encoding UTF8
Write-Host "    Written to: $BackendConfPath" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Bootstrap complete." -ForegroundColor Green
Write-Host ""
Write-Host "Next step:" -ForegroundColor Yellow
Write-Host "  cd infra/terraform"
Write-Host "  terraform init -backend-config=backend.conf"
Write-Host ""
