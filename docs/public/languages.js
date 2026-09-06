/**
 * The single source of truth for the language ports advertised by the site.
 *
 * The playground (`app.js`) answers `GET /languages` from this list, and `build.mjs` renders the
 * cards of the landing page from it, so the install commands only ever exist in one place.
 */
export const LANGUAGES = [
  {
    language: "C#",
    package: "MessageFlow",
    install: "dotnet add package MessageFlow",
    documentation: "https://github.com/charles2ke/Message-Flow#readme",
    runtime: ".NET 8",
    style: "ValueTask",
    card: "dotnet add package MessageFlow",
  },
  {
    language: "Java",
    package: "io.github.charles2ke:messageflow",
    install: "mvn dependency:get -Dartifact=io.github.charles2ke:messageflow:1.0.0",
    documentation: "https://github.com/charles2ke/Message-Flow/blob/main/java/README.md",
    runtime: "Java 17",
    style: "CompletionStage",
    card: "io.github.charles2ke:messageflow",
  },
  {
    language: "Python",
    package: "messageflow",
    install: "pip install messageflow",
    documentation: "https://github.com/charles2ke/Message-Flow/blob/main/python/README.md",
    runtime: "Python 3.9+",
    style: "asyncio",
    card: "pip install messageflow",
  },
  {
    language: "Node",
    package: "@charles2ke/messageflow",
    install: "npm install @charles2ke/messageflow",
    documentation: "https://github.com/charles2ke/Message-Flow/blob/main/node/README.md",
    runtime: "Node 20+, TypeScript",
    style: "Promise",
    card: "npm install @charles2ke/messageflow",
  },
];
