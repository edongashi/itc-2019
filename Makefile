all: linux win osx

linux:
	dotnet publish -c Release -o bin/linux-x64 --self-contained -r linux-x64

win:
	dotnet publish -c Release -o bin/win-x64 --self-contained -r win-x64

osx:
	dotnet publish -c Release -o bin/osx-x64 --self-contained -r osx-x64

clean:
	rm -rf bin
