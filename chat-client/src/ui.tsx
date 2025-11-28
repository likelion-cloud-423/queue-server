import type { Instance } from "ink";
import { render, Box, Text, useInput, useStdout } from "ink";
import cliTruncate from "cli-truncate";
import { EventEmitter } from "node:events";
import { useCallback, useEffect, useMemo, useState, useSyncExternalStore } from "react";
import stringWidth from "string-width";

export interface ChatUiOptions {
  nickname: string;
  serverUrl: string;
}

type InputHandler = (value: string) => void;
type QuitHandler = () => void;

export interface ChatUi {
  start(): void;
  stop(): void;
  onInput(handler: InputHandler): void;
  onQuit(handler: QuitHandler): void;
  showSystem(message: string): void;
  showIncoming(message: string, label?: string): void;
  showOutgoing(message: string): void;
  showPendingOutgoing(clientId: string, message: string): void;
  confirmPendingOutgoing(clientId: string, message?: string, label?: string): void;
  showStatus(message: string): void;
  updateCornerStatus(message: string): void;
}

export function createChatUi(options: ChatUiOptions): ChatUi {
  return new InkChatUi(options);
}

type MessageVariant = "system" | "incoming" | "outgoing" | "status" | "pending";

interface ChatMessage {
  readonly id: number;
  readonly text: string;
  readonly variant: MessageVariant;
  readonly clientId?: string;
  readonly label?: string;
}

interface ChatViewState {
  readonly messages: ChatMessage[];
  readonly statusLine: string;
  readonly serverStatus: string;
}

const DEFAULT_SERVER_STATUS = "[서버 인원] 대기 중...";

class ChatStore {
  private readonly emitter = new EventEmitter();
  private nextId = 1;
  private state: ChatViewState = {
    messages: [],
    statusLine: "",
    serverStatus: DEFAULT_SERVER_STATUS,
  };

  constructor(private readonly options: ChatUiOptions) {}

  getOptions(): ChatUiOptions {
    return this.options;
  }

  getSnapshot = (): ChatViewState => this.state;

  subscribe = (listener: () => void): (() => void) => {
    this.emitter.on("change", listener);
    return () => this.emitter.off("change", listener);
  };

  appendMessage(variant: MessageVariant, text: string, label?: string): void {
    const entry: ChatMessage = { id: this.nextId++, text, variant, label };
    this.setState({
      ...this.state,
      messages: [...this.state.messages, entry],
    });
  }

  appendPendingOutgoing(clientId: string, text: string): void {
    const entry: ChatMessage = { id: this.nextId++, text, variant: "pending", clientId };
    this.setState({
      ...this.state,
      messages: [...this.state.messages, entry],
    });
  }

  confirmPendingOutgoing(clientId: string, message?: string, label?: string): void {
    const index = this.state.messages.findIndex((entry) => entry.clientId === clientId);
    if (index === -1) {
      this.appendMessage("outgoing", message ?? "", label);
      return;
    }
    const updated: ChatMessage[] = this.state.messages.map((entry, idx) => {
      if (idx !== index) {
        return entry;
      }
      return {
        ...entry,
        text: message ?? entry.text,
        variant: "outgoing",
        clientId: undefined,
        label: label ?? entry.label,
      };
    });
    this.setState({ ...this.state, messages: updated });
  }

  setStatusLine(message: string): void {
    this.setState({ ...this.state, statusLine: message });
  }

  setServerStatus(message: string): void {
    this.setState({ ...this.state, serverStatus: message || DEFAULT_SERVER_STATUS });
  }

  private setState(next: ChatViewState): void {
    this.state = next;
    this.emitter.emit("change");
  }
}

class InkChatUi implements ChatUi {
  private readonly store: ChatStore;
  private readonly instance: Instance;
  private inputHandler: InputHandler | null = null;
  private quitHandler: QuitHandler | null = null;
  private stopped = false;

  constructor(options: ChatUiOptions) {
    this.store = new ChatStore(options);
    this.instance = render(
      <ChatScreen store={this.store} onSubmit={this.handleInput} onQuit={this.handleQuit} />,
      { exitOnCtrlC: false },
    );
  }

  start(): void {
    // Ink renders immediately; nothing to do here but kept for interface parity.
  }

  stop(): void {
    if (this.stopped) {
      return;
    }
    this.stopped = true;
    this.instance.unmount();
  }

  onInput(handler: InputHandler): void {
    this.inputHandler = handler;
  }

  onQuit(handler: QuitHandler): void {
    this.quitHandler = handler;
  }

  showSystem(message: string): void {
    this.store.appendMessage("system", message);
  }

  showIncoming(message: string, label?: string): void {
    this.store.appendMessage("incoming", message, label);
  }

  showOutgoing(message: string): void {
    this.store.appendMessage("outgoing", message);
  }

  showPendingOutgoing(clientId: string, message: string): void {
    this.store.appendPendingOutgoing(clientId, message);
  }

  confirmPendingOutgoing(clientId: string, message?: string, label?: string): void {
    this.store.confirmPendingOutgoing(clientId, message, label);
  }

  showStatus(message: string): void {
    this.store.appendMessage("status", message);
    this.store.setStatusLine(message);
  }

  updateCornerStatus(message: string): void {
    this.store.setServerStatus(message);
  }

  private handleInput = (value: string): void => {
    this.inputHandler?.(value);
  };

  private handleQuit = (): void => {
    this.quitHandler?.();
  };
}

interface ChatScreenProps {
  store: ChatStore;
  onSubmit(value: string): void;
  onQuit(): void;
}

function ChatScreen({ store, onSubmit, onQuit }: ChatScreenProps): JSX.Element {
  const state = useChatState(store);
  const [draft, setDraft] = useState("");
  const options = store.getOptions();

  useInput((input, key) => {
    if (key.escape || (key.ctrl && (input === "c" || input === "C"))) {
      onQuit();
    }
  });

  const handleSubmit = useCallback(
    (value: string) => {
      onSubmit(value);
      setDraft("");
    },
    [onSubmit],
  );

  return (
    <Box flexDirection="column" paddingX={1} paddingY={1}>
      <ChatHeader nickname={options.nickname} serverUrl={options.serverUrl} statusLine={state.statusLine} />
      <MessagesPanel messages={state.messages} />
      <InputBar value={draft} onChange={setDraft} onSubmit={handleSubmit} serverStatus={state.serverStatus} />
    </Box>
  );
}

interface ChatHeaderProps {
  nickname: string;
  serverUrl: string;
  statusLine: string;
}

function ChatHeader({ nickname, serverUrl, statusLine }: ChatHeaderProps): JSX.Element {
  return (
    <Box flexDirection="column">
      <Box justifyContent="space-between">
        <Text color="greenBright">Queue Chat</Text>
        <Text color="cyan">{nickname}</Text>
      </Box>
      <Text color="gray">{serverUrl}</Text>
      <Text color="yellow">{statusLine || "상태 정보를 기다리는 중입니다."}</Text>
    </Box>
  );
}

interface MessagesPanelProps {
  messages: ChatMessage[];
}

function MessagesPanel({ messages }: MessagesPanelProps): JSX.Element {
  return (
    <Box flexDirection="column" marginTop={1} gap={0}>
      {messages.length === 0 ? (
        <Text color="gray">아직 대화가 없습니다. 메시지를 입력해 보세요.</Text>
      ) : (
        messages.map((message) => <MessageRow key={message.id} message={message} />)
      )}
    </Box>
  );
}

interface MessageRowProps {
  message: ChatMessage;
}

function MessageRow({ message }: MessageRowProps): JSX.Element {
  const { prefix, color } = useMemo(() => getMessageStyle(message.variant), [message.variant]);
  const label = message.label ?? prefix;
  return (
    <Box>
      <Text color={color}>{label}</Text>
      <Text> {message.text}</Text>
    </Box>
  );
}

interface InputBarProps {
  value: string;
  onChange(value: string): void;
  onSubmit(value: string): void;
  serverStatus: string;
}

function InputBar({ value, onChange, onSubmit, serverStatus }: InputBarProps): JSX.Element {
  return (
    <Box flexDirection="column" marginTop={1}>
      <Text color="magentaBright">메시지 입력</Text>
      <InlineTextInput value={value} onChange={onChange} onSubmit={onSubmit} placeholder="/help 로 명령 보기" />
      <Text color="blueBright">{serverStatus}</Text>
    </Box>
  );
}

function truncateText(value: string, columns: number, position: "start" | "end"): string {
  if (columns <= 0) {
    return "";
  }
  if (!value) {
    return "";
  }
  return cliTruncate(value, columns, { position });
}

function removeLastGlyph(text: string): string {
  const glyphs = Array.from(text);
  glyphs.pop();
  return glyphs.join("");
}

interface InlineTextInputProps {
  value: string;
  onChange(value: string): void;
  onSubmit(value: string): void;
  placeholder?: string;
}

function InlineTextInput({ value, onChange, onSubmit, placeholder }: InlineTextInputProps): JSX.Element {
  const { stdout } = useStdout();
  const [cursorVisible, setCursorVisible] = useState(true);

  useEffect(() => {
    const timer = setInterval(() => setCursorVisible((prev) => !prev), 500);
    return () => clearInterval(timer);
  }, []);

  useInput((input, key) => {
    if (key.return) {
      onSubmit(value);
      return;
    }
    if (key.backspace || key.delete) {
      if (value.length > 0) {
        onChange(removeLastGlyph(value));
      }
      return;
    }
    if (key.ctrl || key.meta || key.escape || key.tab || key.leftArrow || key.rightArrow || key.upArrow || key.downArrow) {
      return;
    }
    if (typeof input === "string" && input.length > 0) {
      onChange(value + input);
    }
  });

  const columns = stdout?.columns ?? 80;
  const cursorChar = cursorVisible ? "▋" : " ";
  const cursorWidth = stringWidth(cursorChar);
  const availableWidth = Math.max(1, columns - cursorWidth - 4);
  const showPlaceholder = value.length === 0;
  const baseText = showPlaceholder ? placeholder ?? "" : value;
  const visibleText = baseText
    ? truncateText(baseText, availableWidth, showPlaceholder ? "end" : "start")
    : "";

  return (
    <Text color={showPlaceholder ? "gray" : undefined}>
      {visibleText}
      {cursorChar}
    </Text>
  );
}

function useChatState(store: ChatStore): ChatViewState {
  return useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);
}

function getMessageStyle(variant: MessageVariant): { prefix: string; color: string } {
  if (variant === "incoming") {
    return { prefix: "상대", color: "cyan" };
  }
  if (variant === "outgoing") {
    return { prefix: "나", color: "green" };
  }
  if (variant === "pending") {
    return { prefix: "나(전송중)", color: "magenta" };
  }
  if (variant === "status") {
    return { prefix: "상태", color: "yellow" };
  }
  return { prefix: "시스템", color: "gray" };
}
