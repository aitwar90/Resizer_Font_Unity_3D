# Resizer_Font_Unity_3D
Resizer fonts Unity 3D
# Resizer_Font_Unity_3D

Based on: https://gist.github.com/nukadelic/47474c7e5d4ee5909462e3b900f4cb82

Zestaw zaawansowanych skryptów edytorskich w języku C# przeznaczonych do dynamicznego skalowania oraz naprawy błędów wyświetlania czcionek w **Unity Editor**. Narzędzia zostały zaprojektowane specjalnie z myślą o programistach pracujących w środowiskach **Linux** (np. Linux Mint / Ubuntu) oraz na monitorach o wysokiej rozdzielczości (**HiDPI/4K**), gdzie domyślny mechanizm skalowania interfejsu Unity często zawodzi, czyniąc napisy mikroskopijnymi lub nieczytelnymi.

## 🛠️ Zawartość zestawu

Repozytorium zawiera cztery wyspecjalizowane skrypty, z których każdy odpowiada za inny podsystem interfejsu Unity:

### 1. EditorFontResizer.cs (`Window -> Editor Font Resizer`)
* **Obszar działania:** Klasyczny system IMGUI (rdzeń edytora Unity).
* **Funkcje:** Agresywnie przeszukuje załadowane style edytora i podbija wielkość czcionki w najważniejszych oknach systemowych: oknie *Project*, drzewie *Hierarchy*, surowych polach zmiennych w *Inspektorze* (`ControlLabel`, `ObjectField`, `TextField`, `NumberField`), sekcjach `[Header]` oraz w oknie *Konsoli*.
* **Sterowanie:** Wyświetla wygodne okno edytorskie z przyciskami `+` oraz `-` do globalnego zoomu. Automatycznie zapisuje konfigurację rozmiarów do lokalnego pliku tekstowego `EditorFontResizer.cfg`.

### 2. LinuxGraphFontResizer.cs (`Window -> Linux VFX-SG Font Resizer`)
* **Obszar działania:** Nowoczesny system kaskadowy **UIElements / GraphView**.
* **Funkcje:** Celuje w okna edytorów wizualnych: **ShaderGraph** oraz **VFX Graph**. 
* **Automatyzacja:** Posiada opcję automatycznego monitorowania w tle (`EditorApplication.update`), która wykrywa otwierające się dynamicznie menu popup oraz selektory, wymuszając na nich natychmiastowe zwiększenie czcionki i wysokości wierszy, zapobiegając ucinaniu tekstu.

### 3. LinuxInspectorFontResizer.cs (`Window -> Linux Inspector Font Resizer`)
* **Obszar działania:** Główne okno systemowe `InspectorWindow`.
* **Funkcje:** Zabezpiecza i wymusza stały, powiększony rozmiar czcionki dla kontenerów `IMGUIContainer` w Inspektorze (miejsca, w których skrypty rysują parametry komponentów i nieliniowych nodów). Nadpisuje surowe właściwości etykiet edytora, by zachować spójność wizualną.

### 4. ShaderGraphResizer.cs (`Window -> Shader Graph Node Menu Fixer`)
* **Obszar działania:** Menu kontekstowe wyszukiwania nodów (`SearchWindow` / `ObjectSelector`).
* **Funkcje:** Rozwiązuje jeden z najbardziej uciążliwych błędów Unity na Linuksie – nakładanie się na siebie i nieczytelność wpisów w menu podręcznym dodawania nodów (**Create Node**). Skrypt nie tylko powiększa czcionkę elementów, ale też proporcjonalnie zwiększa wysokość każdego wiersza (`height`) oraz drastycznie skaluje pole wyszukiwarki na samej górze.

---

## ⚙️ Instalacja

Aby skrypty zaczęły działać w Twoim projekcie, muszą zostać umieszczone w specjalnym folderze systemowym Unity o nazwie **Editor**:

1. Stwórz w katalogu `Assets` swojego projektu folder o nazwie `Editor` (jeśli jeszcze nie istnieje).
2. Skopiuj tam wszystkie 4 pliki `.cs` (lub tylko te, których potrzebujesz).

### Przykładowa, prawidłowa struktura projektu:
```text
Twój_Projekt_Unity/
└── Assets/
    └── Editor/
        ├── EditorFontResizer.cs
        ├── LinuxGraphFontResizer.cs
        ├── LinuxInspectorFontResizer.cs
        └── ShaderGraphResizer.cs
