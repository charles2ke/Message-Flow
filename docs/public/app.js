/**
 * Boots the MessageFlow playground.
 *
 * The OpenAPI operations documented in `openapi.yaml` are answered inside this page: `fetch` is
 * wrapped so that requests to the playground server are routed through a real MessageFlow chain
 * built with the JavaScript port of the library. Nothing is sent over the network.
 */
import {
  ChainLogLevel,
  UnhandledRequestError,
  createChain,
} from "./assets/messageflow/index.js";

const BASE_URL = "https://playground.messageflow.local/v1";

const LANGUAGES = [
  {
    language: "C#",
    package: "MessageFlow",
    install: "dotnet add package MessageFlow",
    documentation: "https://github.com/charles2ke/Message-Flow#readme",
  },
  {
    language: "Java",
    package: "io.github.messageflow:messageflow",
    install: "mvn dependency:get -Dartifact=io.github.messageflow:messageflow:1.0.0",
    documentation: "https://github.com/charles2ke/Message-Flow/blob/main/java/README.md",
  },
  {
    language: "Python",
    package: "messageflow",
    install: "pip install messageflow",
    documentation: "https://github.com/charles2ke/Message-Flow/blob/main/python/README.md",
  },
  {
    language: "Node",
    package: "messageflow",
    install: "npm install messageflow",
    documentation: "https://github.com/charles2ke/Message-Flow/blob/main/node/README.md",
  },
];

const TICKET_KINDS = ["refund", "passwordReset", "other"];
const PRIORITIES = ["normal", "urgent"];

/** Collects the entries written by the logging middleware of a single execution. */
class RecordingLogger {
  constructor() {
    this.entries = [];
  }

  isEnabled() {
    return true;
  }

  log(level, message) {
    this.entries.push(`${ChainLogLevel[level] ?? level}: ${message}`);
  }
}

/** Creates the support ticket triage chain used by the `/chains/ticket-triage` operations. */
function createTicketTriageChain(logger, accepted) {
  return createChain()
    .useLogging(logger, ChainLogLevel.Information, "ticket-triage")
    .useWhen(
      (ticket) => ticket.kind === "refund",
      (ticket) => accepted("refund", `refund issued for ticket ${ticket.id}`),
    )
    .useWhen(
      (ticket) => ticket.kind === "passwordReset",
      (ticket) => accepted("password-reset", `reset link sent for ticket ${ticket.id}`),
    )
    .useBranch(
      (ticket) => ticket.priority === "urgent",
      (branch) =>
        branch.useWhen(
          (ticket) => ticket.kind === "other",
          (ticket) => accepted("escalation", `ticket ${ticket.id} escalated to a human agent`),
        ),
    )
    .withFallback((ticket) => accepted("fallback", `ticket ${ticket.id} queued for triage`))
    .build();
}

const TICKET_TRIAGE_DESCRIPTION = [
  {
    name: "logging",
    kind: "middleware",
    description: "Logs the start, the completion and the failure of every request.",
  },
  { name: "refund", kind: "handler", description: "Accepts tickets of kind `refund`." },
  {
    name: "password-reset",
    kind: "handler",
    description: "Accepts tickets of kind `passwordReset`.",
  },
  {
    name: "escalation",
    kind: "branch",
    description: "Urgent tickets enter a branch that escalates the ones nothing else accepted.",
  },
  {
    name: "fallback",
    kind: "fallback",
    description: "Queues every ticket no handler accepted.",
  },
];

const json = (status, body) => ({ status, body });

const badRequest = (message) => json(400, { error: "ValidationError", message });

function formatResponse(template, request) {
  return String(template).split("{request}").join(String(request));
}

function validateTicket(body) {
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return "The request body must be a ticket object.";
  }

  if (!Number.isInteger(body.id)) {
    return "`id` must be an integer.";
  }

  if (!TICKET_KINDS.includes(body.kind)) {
    return `\`kind\` must be one of ${TICKET_KINDS.join(", ")}.`;
  }

  if (body.priority !== undefined && !PRIORITIES.includes(body.priority)) {
    return `\`priority\` must be one of ${PRIORITIES.join(", ")}.`;
  }

  return null;
}

function validateChainRequest(body) {
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return "The request body must be a chain execution request.";
  }

  if (typeof body.request !== "number" || Number.isNaN(body.request)) {
    return "`request` must be a number.";
  }

  if (!Array.isArray(body.handlers) || body.handlers.length === 0) {
    return "`handlers` must contain at least one handler description.";
  }

  for (const handler of body.handlers) {
    if (handler === null || typeof handler !== "object" || typeof handler.name !== "string") {
      return "Every handler needs a `name`.";
    }

    if (typeof handler.respond !== "string") {
      return `Handler \`${handler.name}\` needs a \`respond\` template.`;
    }

    const condition = handler.when;
    if (condition === null || typeof condition !== "object") {
      return `Handler \`${handler.name}\` needs a \`when\` condition.`;
    }

    const comparisons = Object.keys(condition).filter((key) =>
      ["equals", "lessThan", "greaterThan"].includes(key),
    );

    if (comparisons.length !== 1 || typeof condition[comparisons[0]] !== "number") {
      return `Handler \`${handler.name}\` needs exactly one of \`equals\`, \`lessThan\` or \`greaterThan\` with a numeric value.`;
    }
  }

  if (body.fallback !== undefined && body.fallback !== null && typeof body.fallback !== "string") {
    return "`fallback` must be a string.";
  }

  return null;
}

function matches(condition, request) {
  if (typeof condition.equals === "number") {
    return request === condition.equals;
  }

  if (typeof condition.lessThan === "number") {
    return request < condition.lessThan;
  }

  return request > condition.greaterThan;
}

async function executeTicketTriage(body) {
  const error = validateTicket(body);
  if (error !== null) {
    return badRequest(error);
  }

  const ticket = { id: body.id, kind: body.kind, priority: body.priority ?? "normal" };
  const logger = new RecordingLogger();
  let handledBy = "fallback";
  const accepted = (name, response) => {
    handledBy = name;
    return Promise.resolve(response);
  };

  const chain = createTicketTriageChain(logger, accepted);
  const started = performance.now();
  const response = await chain.execute(ticket);

  return json(200, {
    response,
    handledBy,
    elapsedMilliseconds: Number((performance.now() - started).toFixed(3)),
    log: logger.entries,
  });
}

async function executeAdHocChain(body) {
  const error = validateChainRequest(body);
  if (error !== null) {
    return badRequest(error);
  }

  const logger = new RecordingLogger();
  let handledBy = "fallback";
  const builder = createChain().useLogging(logger, ChainLogLevel.Information, "ad-hoc");

  for (const handler of body.handlers) {
    builder.useWhen(
      (request) => matches(handler.when, request),
      (request) => {
        handledBy = handler.name;
        return Promise.resolve(formatResponse(handler.respond, request));
      },
    );
  }

  if (typeof body.fallback === "string") {
    builder.withFallback((request) => {
      handledBy = "fallback";
      return Promise.resolve(formatResponse(body.fallback, request));
    });
  }

  const chain = builder.build();
  const started = performance.now();

  try {
    const response = await chain.execute(body.request);

    return json(200, {
      response,
      handledBy,
      elapsedMilliseconds: Number((performance.now() - started).toFixed(3)),
      log: logger.entries,
    });
  } catch (failure) {
    if (failure instanceof UnhandledRequestError) {
      return json(422, { error: "UnhandledRequestError", message: failure.message });
    }

    throw failure;
  }
}

/**
 * The playground router — itself a chain of responsibility: every operation is a link, and the
 * fallback produces the 404 response.
 */
const router = createChain()
  .useWhen(
    (call) => call.method === "GET" && call.path === "/languages",
    () => Promise.resolve(json(200, { languages: LANGUAGES })),
  )
  .useWhen(
    (call) => call.method === "GET" && call.path === "/chains/ticket-triage",
    () =>
      Promise.resolve(
        json(200, {
          name: "ticket-triage",
          count: createTicketTriageChain(new RecordingLogger(), () => Promise.resolve("")).count,
          handlers: TICKET_TRIAGE_DESCRIPTION,
        }),
      ),
  )
  .useWhen(
    (call) => call.method === "POST" && call.path === "/chains/ticket-triage/execute",
    (call) => executeTicketTriage(call.body),
  )
  .useWhen(
    (call) => call.method === "POST" && call.path === "/chains/execute",
    (call) => executeAdHocChain(call.body),
  )
  .withFallback((call) =>
    Promise.resolve(
      json(404, {
        error: "NotFound",
        message: `No operation matches ${call.method} ${call.path}.`,
      }),
    ),
  )
  .build();

/** Answers a single playground request. */
export async function handlePlaygroundRequest(method, path, rawBody) {
  let body = null;

  if (rawBody !== undefined && rawBody !== null && rawBody !== "") {
    try {
      body = JSON.parse(rawBody);
    } catch {
      return badRequest("The request body is not valid JSON.");
    }
  }

  try {
    return await router.execute({ method: method.toUpperCase(), path, body });
  } catch (failure) {
    return json(500, {
      error: "InternalError",
      message: failure instanceof Error ? failure.message : String(failure),
    });
  }
}

/** Routes requests aimed at the playground server to {@link handlePlaygroundRequest}. */
export function installPlaygroundServer(scope = globalThis) {
  const originalFetch = scope.fetch.bind(scope);

  scope.fetch = async (input, init) => {
    const request = new Request(input, init);

    if (!request.url.startsWith(BASE_URL)) {
      return originalFetch(input, init);
    }

    const path = new URL(request.url).pathname.slice(new URL(BASE_URL).pathname.length);
    const rawBody = request.method === "GET" || request.method === "HEAD" ? "" : await request.text();
    const { status, body } = await handlePlaygroundRequest(request.method, path, rawBody);

    return new Response(JSON.stringify(body, null, 2), {
      status,
      headers: { "content-type": "application/json" },
    });
  };
}

function boot() {
  const status = document.getElementById("status");

  installPlaygroundServer();

  window.SwaggerUIBundle({
    url: "openapi.yaml",
    dom_id: "#swagger-ui",
    deepLinking: true,
    docExpansion: "list",
    defaultModelsExpandDepth: 0,
    tryItOutEnabled: true,
    presets: [window.SwaggerUIBundle.presets.apis],
    plugins: [window.SwaggerUIBundle.plugins.DownloadUrl],
    layout: "BaseLayout",
    onComplete: () => {
      status.dataset.state = "ready";
      status.textContent =
        "Ready — the endpoints are answered by the MessageFlow JavaScript port running in this page.";
    },
    onFailure: (failure) => {
      status.dataset.state = "error";
      status.textContent = `The API description could not be loaded: ${failure}`;
    },
  });
}

if (typeof document !== "undefined") {
  boot();
}
