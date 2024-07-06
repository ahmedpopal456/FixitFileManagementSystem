# Introduction 
This repository contains:
1. The internal lib used by the service to perform all file-specific operations, including uploading files, persisting them, retrieving temporary SAS tokens, etc. 
3. The CI/CD terraform-based files, and Azure yaml, allowing the deployment of this service
4. The RestAPI interface (deployed in a multi-tenant fashion), allowing for the client application to get relevant data
5. The event-driven Azure trigger functions' project, handling any relevant message on listened topics  

# Getting Started
1.	This project contains the rest api, of which the entry point is the program.cs file
2.	There are internal nuget packages that this service uses, of which all can be found under the Fixit "list"
3.	Official releases of this service are held in a private Azure Devops project, and will be migrated here shortly

# Build and Test
1. For each C# project, there exists an equivalent unit tests project, all under the same solution. 
