#!/bin/bash

# Configuração
RESOURCE_GROUP="eem-resources"
LOCATION="eastus"
STORAGE_ACCOUNT="eemstorage"
COSMOS_ACCOUNT="eem-cosmos"
OPENAI_ACCOUNT="eem-openai"
APP_SERVICE_PLAN="eem-appplan"
APP_SERVICE="eem-mcp-server"

# Criar grupo de recursos
az group create --name $RESOURCE_GROUP --location $LOCATION --tags Project=Eem Program=FoundersHub

# Criar conta de Storage
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --tags Project=Eem Program=FoundersHub

# Criar conta Cosmos DB
az cosmosdb create \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION \
  --capabilities EnableGremlin \
  --tags Project=Eem Program=FoundersHub

# Criar conta OpenAI
az cognitiveservices account create \
  --name $OPENAI_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --kind OpenAI \
  --sku S0 \
  --tags Project=Eem Program=FoundersHub

# Criar plano App Service
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku B1 \
  --is-linux \
  --tags Project=Eem Program=FoundersHub

# Criar App Service
az webapp create \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE|8.0" \
  --tags Project=Eem Program=FoundersHub

# Obter e exibir as chaves
echo "Obtendo chaves para configuração..."
STORAGE_KEY=$(az storage account keys list --resource-group $RESOURCE_GROUP --account-name $STORAGE_ACCOUNT --query [0].value -o tsv)
COSMOS_KEY=$(az cosmosdb keys list --resource-group $RESOURCE_GROUP --name $COSMOS_ACCOUNT --query primaryMasterKey -o tsv)
OPENAI_KEY=$(az cognitiveservices account keys list --resource-group $RESOURCE_GROUP --name $OPENAI_ACCOUNT --query key1 -o tsv)

echo "Azure Storage Connection String:"
echo "DefaultEndpointsProtocol=https;AccountName=$STORAGE_ACCOUNT;AccountKey=$STORAGE_KEY;EndpointSuffix=core.windows.net"

echo "Cosmos DB Endpoint:"
echo "https://$COSMOS_ACCOUNT.documents.azure.com:443/"

echo "Cosmos DB Key:"
echo "$COSMOS_KEY"

echo "OpenAI Endpoint:"
echo "https://$OPENAI_ACCOUNT.openai.azure.com/"

echo "OpenAI Key:"
echo "$OPENAI_KEY"