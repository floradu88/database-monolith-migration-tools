/*
Production recommendation for Azure SQL Database:
1. Create an Azure Storage container.
2. Create a database scoped credential with a SAS token.
3. Replace the ring_buffer target with package0.event_file and an HTTPS blob URL.
4. Grant only the required storage permissions and rotate the SAS.
5. Ingest .xel files into the intelligence catalog and monitor ingestion lag.

Use Microsoft's current Azure SQL Extended Events event_file guidance because credential
and URL requirements depend on the deployment and security model.
*/
