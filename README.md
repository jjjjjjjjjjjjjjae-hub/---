# LargeFileCleaner (Windows)

Windows 10/11 үшін үлкен файлдарды тауып, таңдалғандарын жоюға арналған қолданба.

## Функциялар
- Әдепкіде 1 ГБ-тан үлкен файлдарды іздейді.
- Файл өлшемі мен толық жолын жоюдан бұрын көрсетеді.
- Жою алдында растау сұрайды.
- Жоюды пайдаланушы профиліндегі, жасырын немесе жүйелік емес файлдармен шектейді.
- Әкімші құқығын талап етеді, бірақ бұл қорғаныс шектеулерін алып тастамайды.

**Ескерту:** файлдарды жою деректердің жоғалуына әкелуі мүмкін. Жолын мұқият тексеріңіз.

## EXE жүктеу
GitHub репозиторийіндегі Actions → Build Windows EXE → соңғы сәтті іске қосу → Artifacts → LargeFileCleaner-windows-x64 бөлімінен ZIP артефактіні жүктеп алыңыз. Ішінде LargeFileCleaner.exe болады.

## Жергілікті құрастыру
Windows жүйесінде .NET 8 SDK арқылы:
    dotnet publish LargeFileCleaner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

EXE жолы: bin\Release\net8.0-windows\win-x64\publish\LargeFileCleaner.exe
