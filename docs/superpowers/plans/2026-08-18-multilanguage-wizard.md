# Многоязычный мастер (RU/EN) — план реализации

> **Для исполнителя:** веди по задачам через superpowers:subagent-driven-development
> или superpowers:executing-plans. Шаги отмечаются галочкой (`- [ ]`).

**Цель:** мастер становится двуязычным (русский/английский): два новых экрана
в начале — приветствие и выбор языка, — а выбор языка мгновенно перерисовывает
весь интерфейс, включая тексты, что рождаются в ядре (размеры, описания дисков,
отказы, разметка).

**Устройство:** единый словарь строк в коде (по набору на язык, общий для ядра
и оболочки), служба-одиночка `Localization.Current` с уведомлением о смене.
XAML берёт текст разметочным расширением `{loc:T ключ}` (привязка к индексатору
службы); модели экранов — через `ViewModelBase`, который на смену языка поднимает
`PropertyChanged(null)`; ядро читает `Localization.Current[Keys...]` в момент
вызова. Экран диска перестраивает свой список при смене языка (его строки
собираются заранее), остальное живёт через уведомления.

**Технологии:** .NET (`net48;net8.0-windows` в ядре, WPF в оболочке), xUnit.

## Общие ограничения (действуют в каждой задаче)

- `WindowsPeace.Core` компилируется под `net48;net8.0-windows`; **ссылок на
  `PresentationFramework` в ядре нет** (проверяется тестом). `INotifyPropertyChanged`
  — из `System.ComponentModel`, это BCL, не WPF: класть в ядро можно.
- Предупреждения = ошибки. Ни одного предупреждения.
- Тесты пишутся до реализации.
- Пустой `catch` запрещён.
- Комментарии и тексты интерфейса — по-русски (в коде). Видимый текст теперь
  RU + EN и живёт в словаре, а не литералами.
- Неизвестный ключ службы даёт видимый маркер `⟨ключ⟩`, а не пустоту.
- Числа форматируются `CultureInfo.CurrentCulture` как сейчас (переключение
  культуры чисел — вне этого захода, см. «Открытые вопросы»).

## Карта файлов

**Создаются (ядро):**
- `Core/Localization/Language.cs` — перечисление языков.
- `Core/Localization/ILanguagePack.cs` — набор строк одного языка.
- `Core/Localization/RussianStrings.cs`, `EnglishStrings.cs` — наборы.
- `Core/Localization/Keys.cs` — константы ключей, сгруппированы по областям.
- `Core/Localization/Localization.cs` — служба-одиночка.

**Создаются (оболочка):**
- `Setup/Localization/T.cs` — разметочное расширение `loc:T`.
- `Setup/Pages/WelcomeViewModel.cs` + `WelcomePage.xaml`(`.cs`).
- `Setup/Pages/LanguageViewModel.cs` + `LanguagePage.xaml`(`.cs`).

**Правятся (ядро):** `Storage/ByteSize.cs`, `Selection/SelectionRules.cs`,
`Selection/DeploymentPlanner.cs`, `Storage/FileSystemContentInspector.cs`,
`Storage/Native/Win32StorageSource.cs` (префикс ошибки разметки).

**Правятся (оболочка):** `Infrastructure/ViewModelBase.cs`, `App.xaml`,
`App.xaml.cs`, `Shell/IWizardPage.cs`, `Shell/ShellWindow.xaml`, все страницы
(`RecipePicker*`, `DiskPicker*`, `DiskRowViewModel`, `RecipeRowViewModel`,
`Confirm*`, `Progress*`, `Done*`), `Pages/IWizardChoice.cs`, `Pages/WizardChoice.cs`.

**Тесты:** `Core.Tests/Localization/LocalizationTests.cs`,
`LocalizationCompletenessTests.cs`; `Setup.Tests/WelcomeViewModelTests.cs`,
`LanguageViewModelTests.cs`; правки в существующих тестах моделей (проверка
на обоих языках).

---

# Этап 1. Основа: служба и словарь (видимых изменений ещё нет)

### Задача 1: язык, наборы строк, служба

**Файлы:**
- Создать: `src/WindowsPeace.Core/Localization/Language.cs`,
  `ILanguagePack.cs`, `RussianStrings.cs`, `EnglishStrings.cs`, `Keys.cs`,
  `Localization.cs`
- Тест: `test/WindowsPeace.Core.Tests/Localization/LocalizationTests.cs`

**Интерфейсы (что появляется для остальных задач):**
- `enum Language { Russian, English }`
- `interface ILanguagePack { Language Language { get; } IReadOnlyDictionary<string,string> Strings { get; } }`
- `Localization.Current` — одиночка; `Language Language { get; set; }`;
  `string this[string key]`; `event EventHandler LanguageChanged`;
  реализует `INotifyPropertyChanged` (поднимает `"Item[]"` при смене языка).
- `Keys` — вложенные статические классы с `const string`.

- [ ] **Шаг 1: тест службы**

```csharp
public class LocalizationTests
{
    [Fact] public void По_умолчанию_русский()
        => Assert.Equal(Language.Russian, new Localization().Language);

    [Fact] public void Индексатор_даёт_текст_текущего_языка()
    {
        var loc = new Localization();
        Assert.Equal("Далее", loc[Keys.Common.Next]);
        loc.Language = Language.English;
        Assert.Equal("Next", loc[Keys.Common.Next]);
    }

    [Fact] public void Неизвестный_ключ_даёт_видимый_маркер()
        => Assert.Equal("⟨нет.такого⟩", new Localization()["нет.такого"]);

    [Fact] public void Смена_языка_поднимает_оба_уведомления()
    {
        var loc = new Localization();
        var changed = false; string? prop = null;
        loc.LanguageChanged += (_, _) => changed = true;
        loc.PropertyChanged += (_, e) => prop = e.PropertyName;
        loc.Language = Language.English;
        Assert.True(changed);
        Assert.Equal("Item[]", prop);
    }

    [Fact] public void Присвоение_того_же_языка_молчит()
    {
        var loc = new Localization();
        var count = 0;
        loc.LanguageChanged += (_, _) => count++;
        loc.Language = Language.Russian;
        Assert.Equal(0, count);
    }
}
```

Здесь `new Localization()` — не одиночка, а отдельный экземпляр для теста
(изоляция). Сделать конструктор `internal`/`public`, а `Current` — статическим
свойством поверх него.

- [ ] **Шаг 2: убедиться, что тест падает** (нет типов) — `dotnet test`
- [ ] **Шаг 3: реализация**

```csharp
// Language.cs
namespace WindowsPeace.Core.Localization;
public enum Language { Russian, English }

// ILanguagePack.cs
using System.Collections.Generic;
namespace WindowsPeace.Core.Localization;
public interface ILanguagePack
{
    Language Language { get; }
    IReadOnlyDictionary<string, string> Strings { get; }
}

// Localization.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
namespace WindowsPeace.Core.Localization;

/// <summary>
/// Текущий язык интерфейса и текст по ключу. Язык — окружающее состояние
/// (как культура потока), поэтому одиночка: и статические методы ядра, и оболочка
/// берут текст из одного места. Тесты создают отдельный экземпляр.
/// </summary>
public sealed class Localization : INotifyPropertyChanged
{
    private readonly IReadOnlyDictionary<Language, IReadOnlyDictionary<string, string>> _packs;
    private Language _language = Language.Russian;

    public Localization() : this(new RussianStrings(), new EnglishStrings()) { }

    public Localization(params ILanguagePack[] packs)
    {
        var map = new Dictionary<Language, IReadOnlyDictionary<string, string>>();
        foreach (var pack in packs) map[pack.Language] = pack.Strings;
        _packs = map;
    }

    public static Localization Current { get; } = new();

    public Language Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
            // "Item[]" — соглашение WPF об изменении индексатора: все привязки
            // {loc:T ...} к нему перечитываются.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    /// <summary>Текст по ключу. Пропущенный перевод — видимый маркер, не пустота.</summary>
    public string this[string key]
        => _packs.TryGetValue(_language, out var pack) && pack.TryGetValue(key, out var text)
            ? text
            : "⟨" + key + "⟩";

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
}
```

`Keys.cs`, `RussianStrings.cs`, `EnglishStrings.cs` на этом шаге содержат только
то, что нужно тесту (`Keys.Common.Next`, значения `"Далее"`/`"Next"`). Полные
таблицы наполняются задачами дальше — каждая добавляет **свои** ключи в оба
набора. Образец наборов:

```csharp
// Keys.cs
namespace WindowsPeace.Core.Localization;
public static class Keys
{
    public static class Common { public const string Next = "common.next"; }
    // ...области добавляются по мере задач: Shell, Welcome, Language, Recipe,
    // Disk, Confirm, Progress, Done, Selection, Size, PartitionType,
    // Content, Plan, Error.
}

// RussianStrings.cs
using System.Collections.Generic;
namespace WindowsPeace.Core.Localization;
public sealed class RussianStrings : ILanguagePack
{
    public Language Language => Language.Russian;
    public IReadOnlyDictionary<string, string> Strings { get; } = new Dictionary<string, string>
    {
        [Keys.Common.Next] = "Далее",
    };
}
// EnglishStrings.cs — то же, значения по-английски: [Keys.Common.Next] = "Next".
```

- [ ] **Шаг 4: тест зелёный** — `dotnet test`
- [ ] **Шаг 5: коммит** — `feat: служба языка и словарь строк`

### Задача 2: тест полноты словаря

**Файлы:** Создать `test/WindowsPeace.Core.Tests/Localization/LocalizationCompletenessTests.cs`

- [ ] **Шаг 1: тест** (растёт вместе со словарём и ловит забытый перевод)

```csharp
public class LocalizationCompletenessTests
{
    [Fact] public void Оба_набора_несут_одинаковые_ключи()
    {
        var ru = new RussianStrings().Strings.Keys.OrderBy(k => k);
        var en = new EnglishStrings().Strings.Keys.OrderBy(k => k);
        Assert.Equal(ru, en);
    }

    [Fact] public void Каждый_объявленный_ключ_есть_в_обоих_наборах()
    {
        var ru = new RussianStrings().Strings;
        var en = new EnglishStrings().Strings;
        foreach (var key in DeclaredKeys())
        {
            Assert.True(ru.ContainsKey(key), $"нет русского: {key}");
            Assert.True(en.ContainsKey(key), $"нет английского: {key}");
        }
    }

    // Собирает значения всех const string во вложенных классах Keys рефлексией:
    // опечатка «объявил, но не перевёл» ловится здесь.
    private static IEnumerable<string> DeclaredKeys()
    {
        foreach (var group in typeof(Keys).GetNestedTypes())
            foreach (var field in group.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.FieldType == typeof(string))
                    yield return (string)field.GetRawConstantValue()!;
    }
}
```

- [ ] **Шаг 2–4:** запустить (зелёный при одном ключе), затем коммит
  `test: полнота словаря по обоим языкам`.

### Задача 3: разметочное расширение `loc:T`

**Файлы:** Создать `src/WindowsPeace.Setup/Localization/T.cs`; правка `App.xaml`
(добавить `xmlns:loc="clr-namespace:WindowsPeace.Setup.Localization"`).

**Интерфейс:** `{loc:T ключ}` в XAML → односторонняя привязка к
`Localization.Current[ключ]`, перечитывается на `"Item[]"`.

- [ ] **Шаг 1: тест** (проверяем, что расширение отдаёт привязку к службе)

```csharp
[Fact] public void Расширение_привязывает_к_индексатору_службы()
{
    var target = new TextBlock();
    var expr = new T("common.next").ProvideValue(new ServiceProviderStub(target, TextBlock.TextProperty));
    target.SetValue(TextBlock.TextProperty, expr);
    Assert.Equal(Localization.Current[Keys.Common.Next], target.Text);
}
```

(Если возня с `IServiceProvider`-заглушкой в тесте перевесит пользу — заменить
на проверку «`ProvideValue` вернул `BindingExpressionBase`, `Binding.Path` =
`[common.next]`, `Source` — `Localization.Current`». Тонкое переключение всё
равно проверяется глазами на стенде, задача 19.)

- [ ] **Шаг 3: реализация**

```csharp
using System;
using System.Windows.Data;
using System.Windows.Markup;
using WindowsPeace.Core.Localization;
namespace WindowsPeace.Setup.Localization;

/// <summary>
/// {loc:T ключ} — текст по ключу на текущем языке. Отдаёт привязку к индексатору
/// службы: на смену языка служба поднимает "Item[]", и все такие привязки
/// перечитываются. Привязки WPF держат цель слабо — утечки нет.
/// </summary>
public sealed class T : MarkupExtension
{
    public T() { }
    public T(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Localization.Current,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
```

- [ ] **Шаг 4–5:** зелёный, коммит `feat: разметочное расширение loc:T`.

### Задача 4: `ViewModelBase` слушает смену языка

**Файлы:** Правка `src/WindowsPeace.Setup/Infrastructure/ViewModelBase.cs`;
тест `test/WindowsPeace.Setup.Tests/ViewModelBaseLanguageTests.cs`.

**Интерфейс:** любая модель на `ViewModelBase` на смену языка сама поднимает
`PropertyChanged(null)` — WPF перечитывает все её привязки. Подписка **слабая**
(`WeakEventManager`), иначе короткоживущие строки-диски утекали бы, оставаясь
подписанными на одиночку.

- [ ] **Шаг 1: тест**

```csharp
private sealed class Probe : ViewModelBase { }

[Fact] public void Смена_языка_поднимает_обновление_всех_свойств()
{
    var loc = Localization.Current;
    loc.Language = Language.Russian;
    var vm = new Probe();
    string? prop = "нетронуто";
    ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => prop = e.PropertyName;
    loc.Language = Language.English;
    Assert.True(string.IsNullOrEmpty(prop)); // null или "" = «все свойства»
    loc.Language = Language.Russian; // вернуть для соседних тестов
}
```

- [ ] **Шаг 3: реализация** — добавить в `ViewModelBase`:

```csharp
protected ViewModelBase()
{
    WeakEventManager<Localization, EventArgs>.AddHandler(
        Localization.Current, nameof(Localization.LanguageChanged), OnLanguageChanged);
}

private void OnLanguageChanged(object? sender, EventArgs e) => Raise(null);
```

(`using System.Windows;` для `WeakEventManager`, `using WindowsPeace.Core.Localization;`.)

- [ ] **Шаг 4–5:** зелёный, коммит `feat: модели перечитываются на смене языка`.

---

# Этап 2. Два новых экрана

### Задача 5: экран приветствия

**Файлы:** Создать `WelcomeViewModel.cs`, `WelcomePage.xaml`(`.cs`); тест
`WelcomeViewModelTests.cs`.

**Интерфейс:** `WelcomeViewModel : IWizardPage`. `Title => ""` (шапка пустая —
крупное имя рисует сама страница); `NextTitle => "Далее / Next"` (двуязычно:
язык ещё не выбран, литерал, не из словаря); `CanGoBack => false` (первый экран);
`CanGoNext => true`.

- [ ] **Шаг 1: тест** — приветствие пускает дальше сразу, назад не пускает,
  кнопка двуязычна.

```csharp
[Fact] public void Приветствие_пускает_дальше_и_не_назад()
{
    var vm = new WelcomeViewModel();
    Assert.True(vm.CanGoNext);
    Assert.False(vm.CanGoBack);
    Assert.Equal("Далее / Next", vm.NextTitle);
}
```

- [ ] **Шаг 3: реализация** модели (по образцу `ProgressViewModel`: пустой
  `OnEnter`, событие `CanGoNextChanged` с пустыми `add/remove`). Страница —
  крупный `TextBlock "Windows Peace"` по центру.
- [ ] **Шаг 4–5:** зелёный, коммит `feat: экран приветствия`.

### Задача 6: экран выбора языка

**Файлы:** Создать `LanguageViewModel.cs`, `LanguagePage.xaml`(`.cs`); тест
`LanguageViewModelTests.cs`.

**Интерфейс:**
- `LanguageOption { Language Language; string NativeLabel }` — подпись всегда
  на своём языке («Русский», «English»), не из словаря.
- `IReadOnlyList<LanguageOption> Options` — русский, английский.
- `LanguageOption? Selected { get; set }` — сеттер ставит
  `Localization.Current.Language = value.Language` и поднимает `CanGoNext`.
- `Title => Localization.Current[Keys.Language.Title]` (перещёлкивается при выборе);
  `NextTitle` — по умолчанию из интерфейса (локализованное «Далее», задача 7);
  `CanGoBack => true`; `CanGoNext => Selected is not null`.

- [ ] **Шаг 1: тест**

```csharp
[Fact] public void Пока_язык_не_выбран_дальше_нельзя()
    => Assert.False(new LanguageViewModel().CanGoNext);

[Fact] public void Выбор_английского_переключает_службу_и_пускает_дальше()
{
    Localization.Current.Language = Language.Russian;
    var vm = new LanguageViewModel();
    vm.Selected = vm.Options.Single(o => o.Language == Language.English);
    Assert.True(vm.CanGoNext);
    Assert.Equal(Language.English, Localization.Current.Language);
    Localization.Current.Language = Language.Russian; // вернуть
}
```

- [ ] **Шаг 3: реализация.** Страница — `ListView` по образцу
  `RecipePickerPage` (обязателен `IsSynchronizedWithCurrentItem="False"`,
  `SelectedItem="{Binding Selected}"`), столбец с `NativeLabel`.
- [ ] **Шаг 4–5:** зелёный, коммит `feat: экран выбора языка`.

### Задача 7: встроить экраны и локализовать кнопку «Далее» по умолчанию

**Файлы:** правка `App.xaml.cs` (навигатор), `App.xaml` (шаблоны данных для
двух новых моделей), `Shell/IWizardPage.cs` (локализованный `NextTitle` по
умолчанию), `Pages/IWizardChoice.cs` + `WizardChoice.cs` (запомнить язык).

**Интерфейс:** навигатор начинается с `WelcomeViewModel`, `LanguageViewModel`,
далее прежние пять. `IWizardPage.NextTitle` по умолчанию =
`Localization.Current[Keys.Common.Next]`. `IWizardChoice.SystemLanguage` —
язык, выбранный на экране (пойдёт в шаг В).

- [ ] **Шаг 1: тесты** — порядок экранов (первый — приветствие); `WizardChoice.SystemLanguage`
  повторяет выбор языка.
- [ ] **Шаг 3: реализация.**
  - `IWizardPage`: `string NextTitle => Localization.Current[Keys.Common.Next];`
    (заменяет литерал `"Далее"`). `ConfirmViewModel` уже перекрывает `NextTitle`
    (задача 10 переведёт на ключ).
  - `App.xaml`: два `DataTemplate` (`WelcomeViewModel`→`WelcomePage`,
    `LanguageViewModel`→`LanguagePage`).
  - `App.xaml.cs`: создать `welcome`, `language` и поставить их первыми в списке
    навигатора; `WizardChoice` получает `LanguageViewModel`, `SystemLanguage =>
    _language.Selected?.Language ?? Language.Russian`.
  - `IWizardChoice`: добавить `Language SystemLanguage { get; }`.
- [ ] **Шаг 4–5:** зелёный, коммит `feat: приветствие и выбор языка в потоке мастера`.

---

# Этап 3. Перевод оболочки (после этого видно переключение на всех экранах Setup)

Каждая задача: заменить литералы на `{loc:T ключ}` (XAML) или
`Localization.Current[Keys...]` (модели), добавить ключи в **оба** набора,
переписать/дополнить тесты моделей на проверку обоих языков. Значения ниже —
готовые, вставляются в `RussianStrings`/`EnglishStrings`.

### Задача 8: шапка и нижний ряд оболочки

**Файлы:** `Shell/ShellWindow.xaml` (кнопки «Назад», «Выйти из установщика» →
`loc:T`). `ShellViewModel.Title`/`NextTitle` уже сквозные (текст даёт страница) —
менять не надо; шапка перещёлкивается, т.к. `ShellViewModel` на `ViewModelBase`.

Ключи:

| Ключ | RU | EN |
|---|---|---|
| `shell.back` | Назад | Back |
| `shell.exit` | Выйти из установщика | Exit installer |

- [ ] Тест (по возможности) — либо глазами на стенде (задача 19). Коммит
  `feat: перевод шапки и выхода оболочки`.

### Задача 9: экран «Что ставим»

**Файлы:** `RecipePickerViewModel.cs` (заголовок → ключ; **`Trouble` из состояния,
а не готовой строкой** — см. ниже), `RecipePickerPage.xaml` (вступление и заголовки
столбцов → `loc:T`), `RecipeRowViewModel.cs` (без изменений: `Size` уже
через `ByteSize`, переведётся в задаче 14).

**Почему `Trouble` иначе.** Опись читается на старте, **до** выбора языка
(`App.OnStartup`). Если сложить сообщение строкой там, оно застынет по-русски.
Поэтому `RecipePickerViewModel` хранит `MediaManifestStatus` (+ версии для
`TooNew`) и признак «носитель не найден», а `Trouble` — **свойство-геттер**,
возвращающее `Localization.Current[ключ по состоянию]`. Тогда текст рождается
на языке показа (экран третий, язык выбран вторым) и перещёлкивается. Сообщения
`MediaManifestReader` в ядре не трогаем — они остаются для журнала.

Ключи:

| Ключ | RU | EN |
|---|---|---|
| `recipe.title` | Что ставим? | What to install? |
| `recipe.intro` | Выберите, что установить. В списке - то, что записано на этом носителе. | Choose what to install. The list shows what is on this media. |
| `recipe.col.name` | Рецепт | Recipe |
| `recipe.col.image` | Издание | Edition |
| `recipe.col.size` | Объём | Size |
| `recipe.col.what` | Что это | What it is |
| `recipe.trouble.notFound` | Носитель Windows Peace не найден: похоже, мастер запущен не с него. Ставить отсюда нечего. | Windows Peace media not found: the wizard seems to be running from elsewhere. Nothing to install here. |
| `recipe.trouble.damaged` | Опись носителя испорчена: прочитать её не получается. Установить с этого носителя ничего нельзя - его нужно записать заново. | The media manifest is damaged and cannot be read. Nothing can be installed from this media - it must be rewritten. |
| `recipe.trouble.tooNew` | Носитель собран более новой версией Windows Peace. Установить с него нельзя - нужен мастер посвежее. | This media was built by a newer version of Windows Peace. It cannot be used - a newer wizard is required. |
| `recipe.trouble.noRecipes` | На носителе нет ни одного рецепта: ставить с него нечего. | The media has no recipes: nothing to install. |

- [ ] Тесты: `recipe.title` и `Trouble` на RU и EN (для каждого состояния).
  Коммит `feat: перевод экрана выбора рецепта`.

### Задача 10: экран «Проверьте и подтвердите»

**Файлы:** `ConfirmViewModel.cs` (`Title`, `NextTitle`, `PlanEffect`, `Trouble`
→ ключи; значения-данные `RecipeName`/`DiskModel` не трогаем), `ConfirmPage.xaml`
(подписи → `loc:T`).

| Ключ | RU | EN |
|---|---|---|
| `confirm.title` | Проверьте и подтвердите | Review and confirm |
| `confirm.install` | Установить | Install |
| `confirm.whatLabel` | Что ставим | What we install |
| `confirm.whereLabel` | Куда ставим | Where we install |
| `confirm.effectLabel` | Что будет сделано | What will happen |
| `confirm.wipe` | Диск будет размечен заново. Всё, что на нём сейчас есть, исчезнет безвозвратно. | The disk will be repartitioned. Everything on it will be lost permanently. |
| `confirm.lostChoice` | Выбор потерялся: вернитесь назад и укажите, что ставим и куда. | The selection was lost: go back and choose what to install and where. |

- [ ] Тесты на обоих языках. Коммит `feat: перевод экрана подтверждения`.

### Задача 11: экран «Куда установить Windows Peace?»

**Файлы:** `DiskPickerViewModel.cs` (заголовок → ключ, **переименован** на
«Куда установить Windows Peace?»; `StatusText`, `EnumerationError` → ключи;
**перестроение списка при смене языка** — см. ниже), `DiskPickerPage.xaml`
(кнопки и заголовки столбцов → `loc:T`), `DiskRowViewModel.cs` (все описания
через ключи).

**Перестроение.** Строки-диски собираются заранее (в `BuildRows`), и их текст
застывает на языке сборки. Поэтому `DiskPickerViewModel` запоминает язык
построения; в `OnEnter`, если `Localization.Current.Language` изменился с тех пор,
перестраивает список (`RefreshAsync`/`BuildRows` заново). Так возврат к выбору
языка, смена и путь обратно дают список на новом языке; вердикты
(`SelectionRules`) при этом тоже пересчитываются. Логика допустимости
(`IsSelectable`) от языка не зависит.

```csharp
private Language _builtLanguage = Localization.Current.Language;

public void OnEnter()
{
    if (Localization.Current.Language != _builtLanguage && !IsBusy)
    {
        _builtLanguage = Localization.Current.Language;
        _ = RefreshAsync();
        return;
    }
    if (Rows.Count == 0 && EnumerationError is null && !IsBusy)
        _ = RefreshAsync();
}
```

(`_builtLanguage` присваивать и в конце `RefreshAsync`, чтобы отражал последнюю сборку.)

Ключи (модель и разметка):

| Ключ | RU | EN |
|---|---|---|
| `disk.title` | Куда установить Windows Peace? | Where to install Windows Peace? |
| `disk.col.name` | Имя | Name |
| `disk.col.size` | Объём | Size |
| `disk.col.free` | Свободно | Free |
| `disk.col.type` | Тип | Type |
| `disk.col.state` | Состояние | State |
| `disk.refresh` | Обновить | Refresh |
| `disk.cancel` | Прервать | Stop |
| `disk.create` | Создать | Create |
| `disk.delete` | Удалить | Delete |
| `disk.format` | Форматировать | Format |
| `disk.extend` | Расширить | Extend |
| `disk.details` | Подробно | Details |
| `disk.loadDriver` | Загрузить драйвер | Load driver |
| `disk.status.enumerating` | Опрашиваю диски… | Scanning disks… |
| `disk.status.inspecting` | Смотрю, что лежит на диске {0} из {1}… | Inspecting disk {0} of {1}… |
| `disk.status.locating` | Ищу загрузочный носитель… | Looking for boot media… |
| `disk.error.cancelled` | Опрос дисков прерван. Нажмите «Обновить», чтобы попробовать снова. | Disk scan stopped. Press "Refresh" to try again. |
| `disk.freeSpace` | Незанятое пространство | Unallocated space |
| `disk.partition` | Раздел {0} | Partition {0} |
| `disk.partitionLabel` | Раздел {0}: {1} | Partition {0}: {1} |
| `disk.note.media` | Загрузочный носитель - установка сюда невозможна | Boot media - cannot install here |
| `disk.note.system` | Здесь работает текущая система | The current system runs here |
| `disk.note.empty` | Пустой | Empty |
| `disk.note.partitions` | Разделов: {0} | Partitions: {0} |
| `parttype.efi` | Системный EFI | EFI system |
| `parttype.msr` | MSR | MSR |
| `parttype.recovery` | Восстановление | Recovery |
| `parttype.basic` | Основной | Basic |
| `parttype.unknown` | Неизвестный | Unknown |
| `content.windowsAndFiles` | Windows и файлы пользователя | Windows and user files |
| `content.windows` | Windows | Windows |
| `content.userFiles` | Файлы пользователя | User files |

Строки с `{0}`/`{1}` форматируются `string.Format(CultureInfo.CurrentCulture,
Localization.Current[ключ], …)` на месте вызова.

- [ ] Тесты: описания строк и заголовок на RU/EN; перестроение при смене языка
  (сменить `Localization.Current.Language`, вызвать `OnEnter`, проверить, что
  строки на новом языке). Коммит `feat: перевод экрана выбора диска`.

### Задача 12: экраны «Установка» и «Готово»

**Файлы:** `ProgressViewModel.cs`, `DoneViewModel.cs` (`Title`, `Explanation`
→ ключи); XAML не трогаем (там `{Binding Explanation}`).

| Ключ | RU | EN |
|---|---|---|
| `progress.title` | Установка | Installation |
| `progress.explanation` | Здесь пойдёт разметка диска, распаковка Windows, установка драйверов и загрузчика. Это появится на следующем шаге работы над программой. Сейчас мастер ничего не записывает на диск. | This is where partitioning, Windows extraction, driver and bootloader install will run. It arrives in the next step of the program's development. For now the wizard writes nothing to disk. |
| `done.title` | Готово | Done |
| `done.explanation` | Здесь будет итог установки и кнопка перезагрузки. Это появится на следующем шаге работы над программой. | This will show the installation result and a restart button. It arrives in the next step of the program's development. |

- [ ] Тесты на обоих языках. Коммит `feat: перевод экранов установки и завершения`.

### Задача 13: сообщение о неожиданной ошибке

**Файлы:** `App.xaml.cs` (`MessageBox.Show` — заголовок и текст через ключи;
строки `Checkpoint(...)` — **журнал, остаются по-русски**).

| Ключ | RU | EN |
|---|---|---|
| `error.title` | Windows Peace | Windows Peace |
| `error.body` | Windows Peace не смог продолжить работу и сейчас закроется.\n\nРазбираться с этим нам, а не вам: что случилось, записано в журнал работы. | Windows Peace could not continue and will now close.\n\nThis is ours to sort out, not yours: what happened is recorded in the work log. |

(Переводы строк собирать `Environment.NewLine`, как сейчас, а не хранить `\n` в словаре.)

- [ ] Коммит `feat: перевод сообщения о сбое`.

---

# Этап 4. Перевод ядра (после этого английский цельный)

### Задача 14: единицы объёма

**Файлы:** `Storage/ByteSize.cs`; тест `ByteSizeTests` (или существующий) на обоих языках.

| Ключ | RU | EN |
|---|---|---|
| `size.gb` | ГБ | GB |
| `size.mb` | МБ | MB |
| `size.lessThanMb` | менее 1 МБ | less than 1 MB |

`Format`: `... .ToString("0.#", CultureInfo.CurrentCulture) + " " + Localization.Current[Keys.Size.Gb]`;
меньше мегабайта — `Localization.Current[Keys.Size.LessThanMb]`; иначе `... + " " + Localization.Current[Keys.Size.Mb]`.

- [ ] Тесты: `Format(...)` даёт «… ГБ» и «… GB». Коммит `feat: перевод единиц объёма`.

### Задача 15: отказы и предупреждения выбора

**Файлы:** `Selection/SelectionRules.cs` (отказы `EvaluateDisk`/`EvaluatePartition`/
`EvaluateSize` и предупреждения `Warnings` → ключи). Логика неизменна.

| Ключ | RU | EN |
|---|---|---|
| `sel.denyMedia` | Это загрузочный носитель Windows Peace - установка сюда невозможна | This is the Windows Peace boot media - installation here is impossible |
| `sel.denySystem` | На этом диске работает текущая система | The current system runs on this disk |
| `sel.denyOffline` | Диск отключён | The disk is offline |
| `sel.denyReadOnly` | Диск защищён от записи | The disk is write-protected |
| `sel.denyService` | Это служебный раздел, система создаёт его сама | This is a service partition; the system creates it itself |
| `sel.denyUnknownTarget` | Неизвестный вид цели | Unknown target kind |
| `sel.tooSmall` | Слишком мало места: не хватает {0} ГБ до минимальных 40 ГБ | Not enough space: {0} GB short of the minimum 40 GB |
| `warn.windowsOnTarget` | На цели установлена Windows. Она будет удалена безвозвратно. | Windows is installed on the target. It will be deleted permanently. |
| `warn.userFilesOnTarget` | На цели есть файлы пользователя. Они будут удалены безвозвратно. | The target has user files. They will be deleted permanently. |
| `warn.partitionsNotRead` | Разделы этого диска прочитать не удалось, поэтому неизвестно, что на нём лежит. | This disk's partitions could not be read, so its contents are unknown. |
| `warn.contentNotInspected` | Содержимое части разделов проверить не удалось: у них нет буквы диска. | Some partitions could not be inspected: they have no drive letter. |
| `warn.weakIdentity` | У диска не удалось прочитать серийный номер, опознать его надёжно нельзя. | The disk's serial number could not be read; it cannot be identified reliably. |
| `warn.otherWindows` | На другом диске найдена установленная Windows. Она может перехватывать загрузку. | Windows was found on another disk. It may hijack booting. |

(«40 ГБ» в `sel.tooSmall` оставить в строке; при желании вынести числом позже.
Предупреждения на экранах шага Б не показываются, но текст переводим — он
пойдёт в дело на шаге В и не должен остаться островом по-русски.)

- [ ] Тесты: отказы на RU/EN. Коммит `feat: перевод правил выбора`.

### Задача 16: предпросмотр разметки

**Файлы:** `Selection/DeploymentPlanner.cs` (заголовок шага «Восстановление» и
хвост одиночной установки → ключи; «EFI»/«MSR»/«Windows» — имена, не переводим).

| Ключ | RU | EN |
|---|---|---|
| `plan.recovery` | Восстановление | Recovery |
| `plan.singleTail` | . Остальные разделы не изменяются. | . Other partitions are unchanged. |

Одиночная сводка: `"Windows " + ByteSize.Format(...) + Localization.Current[Keys.Plan.SingleTail]`.

- [ ] Тесты на обоих языках. Коммит `feat: перевод предпросмотра разметки`.

### Задача 17: причины «содержимое не проверено» и ошибка разметки

**Файлы:** `Storage/FileSystemContentInspector.cs` (четыре причины `NotInspected`
→ ключи), `Storage/PartitionInfo.cs` («Содержимое ещё не проверялось» — там же),
`Storage/Native/Win32StorageSource.cs` (префикс «Разметку прочитать не удалось,
код ошибки …» — перевести префикс, код оставить).

| Ключ | RU | EN |
|---|---|---|
| `content.notInspected.cancelled` | Проверка прервана | Inspection cancelled |
| `content.notInspected.service` | Служебный раздел, содержимое не проверяется | Service partition; contents are not inspected |
| `content.notInspected.noLetter` | У раздела нет буквы диска | The partition has no drive letter |
| `content.notInspected.pending` | Содержимое ещё не проверялось | Contents not inspected yet |
| `layout.readFailed` | Разметку прочитать не удалось, код ошибки {0} | Could not read the layout, error code {0} |

(`WmiDiskEnumerator` не используется приложением — правим только если легко;
иначе оставляем, помечаем в отчёте.)

- [ ] Тесты. Коммит `feat: перевод причин непроверенного содержимого`.

---

# Этап 5. Проверка

### Задача 18: полнота и стенд на обоих языках

- [ ] `dotnet test` — всё зелёное, без предупреждений.
- [ ] Снимки стенда (`Show-PeaceApp.ps1`) приветствия, выбора языка и хотя бы
  выбора рецепта/диска/сводки **на русском и на английском**: пройти мастер
  `-Advance`, на экране языка выбрать RU и EN, снять оба. Островов по-русски
  в английском режиме быть не должно; маркеров `⟨ключ⟩` — тоже.
- [ ] Проход только клавиатурой (стрелки в списке языка/диска, Tab, Enter).
- [ ] Отчёт автору: что переведено, где остались осознанные исключения
  (аббревиатуры SSD/HDD/Sata/Usb, имена EFI/MSR/Windows), снимки RU/EN.

---

## Самопроверка плана против спеки

- **Служба + словарь в коде, общий для ядра и оболочки** — задачи 1–2. ✔
- **`Localization.Current`, `LanguageChanged`, индексатор, маркер `⟨key⟩`** — задача 1. ✔
- **Тест полноты (парность + используемые ключи)** — задача 2. ✔
- **Разметочное расширение** — задача 3; **`ViewModelBase` поднимает обновление** — задача 4. ✔
- **Экраны приветствия и выбора языка, встроены первыми** — задачи 5–7. ✔
- **Перевод всех видимых текстов оболочки** — задачи 8–13. ✔
- **Перевод текстов ядра (размеры, отказы, типы, разметка, содержимое)** — задачи 14–17. ✔
- **Переименование заголовка диска** — задача 11. ✔
- **Живое переключение** — расширение + `ViewModelBase` + перестроение диска. ✔
- **Запоминание языка для шага В** — `IWizardChoice.SystemLanguage`, задача 7. ✔
- **Проверка на обоих языках (тесты + стенд)** — по задачам и задача 18. ✔

## Осознанные отступления и решения (сверить с автором)

1. **Сообщения об ошибке описи** показываются из состояния (`RecipePickerViewModel`
   по `MediaManifestStatus`), а не готовой строкой из ядра: опись читается **до**
   выбора языка, готовая строка застыла бы по-русски. Строки `MediaManifestReader`
   остаются для журнала. (Спека §4 это прямо не оговаривала — решаю так.)
2. **Экран диска перестраивает список** при смене языка (спека §4). Остальные
   экраны обходятся живыми геттерами + уведомлением, без перестроения.
3. **Не переводим** технические аббревиатуры шины/носителя (`Sata`, `Usb`,
   `SSD`, `HDD`) и имена разделов `EFI`/`MSR`/`Windows` — это обозначения, не текст.
4. **Число в объёме** форматируется по `CurrentCulture`, как сейчас; переводим
   только слово единицы. Полное переключение культуры чисел/дат — вне захода.

## Открытые вопросы

1. Язык устанавливаемой Windows = язык мастера? Пока `IWizardChoice.SystemLanguage`
   повторяет выбор; развяжем на шаге В при надобности.
2. Раскладка клавиатуры и формат времени (их спрашивает настоящий установщик)
   — не входят, добавятся тем же механизмом позже.
3. Изоляция тестов: `Localization.Current` — одиночка с изменяемым языком.
   Тесты, читающие ядро на конкретном языке, ставят язык явно и возвращают
   русский в конце; при нужде — общий `[Collection]`, чтобы не шли параллельно.
