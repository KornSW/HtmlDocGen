# Technical debt

## Compatibility

- V2 dropped the VB implementation, the net46 target, the dependencies kSystemExtensions / ComponentDiscovery and the wkhtmltopdf runtimes and build targets. The previous implementation remains available on master. The changelog marks this as incompatible with 1.x without KVU trigger words, because the major version is set by the 2.0.0 baseline in versioninfo.json (first release: 2.0.1).

## Packaging

- The nuspec ships the root LICENSE via license type="file" (MIT + Commons Clause has no SPDX identifier). Not yet verified with a real nuget pack.

## T4 snapshot

- The checked-in snapshot was generated with TextTransform.exe from the local working tree because the V2 sources were not yet published. The source URLs in the snapshot point to the canonical V2 branch; regenerate once after publishing (see backlog).
