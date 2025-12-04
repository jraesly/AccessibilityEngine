# AccessibilityEngine

AccessibilityEngine is a set of .NET projects that analyze UI artifacts for accessibility issues. The workspace contains core engine components, rule implementations, adapters, and function-hosted entry points used for scanning solutions.

## Prerequisites

- .NET 8 SDK

## Projects

- `AccessibilityEngine.Core` - Core engine and models
- `AccessibilityEngine.Rules` - Accessibility rule implementations
- `AccessibilityEngine.AI` - (Optional) AI evaluator integration
- `AccessibilityEngine.Functions` - Azure Functions / CLI entry points for scanning

## Build

From the repository root run:

```
dotnet build
```

## Tests

Run unit tests with:

```
dotnet test
```

To run a specific test project:

```
dotnet test AccessibilityEngine.Tests/AccessibilityEngine.Tests.csproj
```

## Running

Some entrypoints are available as regular .NET projects. To run the Functions project locally:

```
dotnet run --project AccessibilityEngine.Functions
```

Adjust commands as needed for your environment.

## Contributing

Contributions are welcome. Please open issues or pull requests on the repository.

## License

See repository for license information.
