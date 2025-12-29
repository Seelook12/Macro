## Gemini Added Memories
- The user is Korean and prefers responses in Korean.
- I should explain code changes in my response instead of adding inline comments.
- If I find something strange or incorrect in the code, I must explain it to the user before making any changes.
- The user is a C# developer.
- The user prefers code to be structured in the MVVM pattern.
- Do not perform general refactoring on multiple files based on a broad instruction. Only refactor the specific file the user is currently asking about, and only after confirming the refactoring task itself.
- Project Code Conventions to strictly adhere to:
1. **Indent**: 4 spaces.
2. **Braces**: Allman style (open brace on new line) for classes, methods, and control blocks.
3. **Naming**: PascalCase for Classes, Methods, Properties; camelCase for local variables and parameters; Interfaces start with 'I'.
4. **Architecture**: MVVM pattern using ReactiveUI (`ViewModelBase` inherits `ReactiveObject`).
5. **Structure**: Use `#region` ... `#endregion` to organize code blocks (Constructor, Properties, Methods).
6. **Comments**: Korean comments are permitted and encouraged for explanations.
- MANDATORY: Always respond in Korean. Explain code changes and logic in Korean.
