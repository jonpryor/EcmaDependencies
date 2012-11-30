CONFIGURATION = Debug

all: missing.txt

clean:
	-rm -Rf bin obj

missing.txt: bin/$(CONFIGURATION)/GetMissingTypes.exe
	mono --debug $< > $@

bin/$(CONFIGURATION)/GetMissingTypes.exe: src/GetMissingTypes.cs obj/$(CONFIGURATION)/Ecma335Types.cs
	mkdir -p "`dirname "$@"`"
	mcs -debug+ /out:$@ $^

obj/$(CONFIGURATION)/Ecma335Types.cs: bin/$(CONFIGURATION)/GetEcma335Types.exe
	mkdir -p "`dirname "$@"`"
	mono $< lib/CLILibraryTypes.xml > $@

bin/$(CONFIGURATION)/GetEcma335Types.exe: src/GetEcma335Types.cs
	mkdir -p "`dirname "$@"`"
	mcs -debug+ $< -out:$@ -r:System.Xml.Linq.dll

