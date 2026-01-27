# Cache Server (.NET)

## Overview
The Cache Server is a TCP-based remote caching service implemented in .NET and hosted as a Windows Service. It is the authoritative component responsible for storage, eviction, expiration, and concurrency control.

## Features
- TCP-based communication
- Thread-safe cache operations
- Configurable cache capacity
- Centralized configuration via appsettings.json
- Logging using log4net
- Windows Service hosting
- Designed for extensibility (TTL, LFU, events)

## Architecture
Client Applications -> TCP -> Cache Server -> Cache Manager

## Configuration
Example appsettings.json:
{
  "CacheSettings": {
    "Port": 5050,
    "MaxItems": 1000
  }
}

## Thread Safety
The server is designed to handle concurrent client connections. Cache operations are synchronized to avoid race conditions.

## Logging
Uses log4net for informational and error logging. Log levels are configurable.

## Running as Windows Service
1. Publish:
   dotnet publish -c Release -o C:\Services\CacheServer
2. Create service:
   sc create CacheServer binPath= "C:\Services\CacheServer\CacheServer.exe"
3. Start service:
   sc start CacheServer

## Testing
- Telnet / PowerShell TCP tests
- Client test console application
- Unit tests for cache manager

## Summary
The Cache Server provides a robust, extensible, and production-ready remote caching solution.
