# ECMA Dependencies

When proposing new types to standardize, it's useful to have a full and
complete closure of types and members that depend on other types and members
which have:

1. previously been standardized, or
2. are being standardized at the same time.

The `src/GetEcma335Types.cs` program reads the [ECMA 335][1] 
`CLILibraryTypes.xml` file to determine which types have already been
standardized (category 1).

[1]: http://www.ecma-international.org/publications/standards/Ecma-335.htm

The `src/GetMissingTypes.cs` program uses the ECMA 335 types and uses
`System.Reflection` against the proposed types to find the full closure of
types that are referenced.

