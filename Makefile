CONFIGURATION = Debug
MSBUILD       = xbuild
RUNTIME       = mono --debug

all: missing.txt

clean:
	-$(MSBUILD) /t:Clean

missing.txt: bin/$(CONFIGURATION)/GetMissingTypes.exe
	$(RUNTIME) $< > $@

bin/$(CONFIGURATION)/GetMissingTypes.exe: src/MissingTypes/GetMissingTypes.cs
	$(MSBUILD)

