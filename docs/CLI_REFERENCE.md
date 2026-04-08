# CLI Reference — `hfdownload`

The `hfdownload` CLI tool provides a command-line interface for downloading, managing, and inspecting cached models from [Hugging Face Hub](https://huggingface.co).

## Installation

```bash
dotnet tool install -g ElBruno.HuggingFace.Downloader.Cli
```

After installation, the `hfdownload` command is available globally.

## Commands

### `download` — Download files from a Hugging Face repository

```bash
hfdownload download <repo-id> <files...> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<repo-id>` | Hugging Face repository ID (e.g., `microsoft/Phi-4-mini-instruct-onnx`) |
| `<files>` | One or more files to download, relative to the repo root |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --output <dir>` | Local directory for downloaded files | Platform cache dir |
| `-r, --revision <ref>` | Git revision — branch, tag, or commit SHA | `main` |
| `-t, --token <token>` | Hugging Face auth token (overrides `HF_TOKEN` env var) | — |
| `--optional` | Treat listed files as optional (skip failures) | `false` |
| `--no-progress` | Suppress progress bar output | `false` |
| `-q, --quiet` | Minimal output (only errors) | `false` |

**Examples:**

```bash
# Download specific files from a public repo
hfdownload download sentence-transformers/all-MiniLM-L6-v2 onnx/model.onnx tokenizer.json

# Download to a specific directory
hfdownload download microsoft/Phi-4-mini-instruct-onnx model.onnx -o ./my-models

# Download from a private repo with token
hfdownload download my-org/private-model weights.bin -t hf_your_token

# Download optional files (skip on failure)
hfdownload download my-org/model config.json --optional

# Quiet mode for scripts
hfdownload download my-org/model weights.bin -q
```

---

### `check` — Check if files exist in the local cache

```bash
hfdownload check <repo-id> <files...> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<repo-id>` | Hugging Face repository ID |
| `<files>` | Files to check |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `-o, --output <dir>` | Local directory to check | Platform cache dir |

**Output:** Shows ✅ for present files and ❌ for missing files, plus a summary.

**Exit codes:** `0` if all files present, `1` if any missing.

**Example:**

```bash
hfdownload check sentence-transformers/all-MiniLM-L6-v2 onnx/model.onnx tokenizer.json
# ✅ onnx/model.onnx
# ❌ tokenizer.json
# 1 of 2 files present
```

---

### `list` — List downloaded models

```bash
hfdownload list [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--cache-dir <dir>` | Cache directory to scan | Platform default |
| `--format <table\|json>` | Output format | `table` |

**Example:**

```bash
hfdownload list
# ┌──────────────────────────────┬───────┬──────────┬──────────────────┐
# │ Model                        │ Files │     Size │ Last Modified    │
# ├──────────────────────────────┼───────┼──────────┼──────────────────┤
# │ sentence-transformers_all-…  │     3 │  85.2 MB │ 2025-01-15 10:30 │
# │ microsoft_phi-4-mini-…       │     2 │   2.1 GB │ 2025-01-14 09:15 │
# └──────────────────────────────┴───────┴──────────┴──────────────────┘

hfdownload list --format json
```

---

### `info` — Show details of a cached model

```bash
hfdownload info <repo-id> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<repo-id>` | Repository ID of the cached model |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--cache-dir <dir>` | Cache directory | Platform default |
| `--format <table\|json>` | Output format | `table` |

**Example:**

```bash
hfdownload info sentence-transformers/all-MiniLM-L6-v2
```

---

### `delete` — Delete a cached model

```bash
hfdownload delete <repo-id> [options]
```

Deletes all files for the specified model. Prompts for confirmation unless `--force` is used.

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--cache-dir <dir>` | Cache directory | Platform default |
| `-f, --force` | Skip confirmation prompt | `false` |

**Example:**

```bash
# Interactive (with confirmation)
hfdownload delete sentence-transformers/all-MiniLM-L6-v2

# Non-interactive (for scripts)
hfdownload delete sentence-transformers/all-MiniLM-L6-v2 --force
```

---

### `delete-file` — Delete a single file from a cached model

```bash
hfdownload delete-file <repo-id> <file> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<repo-id>` | Repository ID of the cached model |
| `<file>` | File to delete, relative to the model directory |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--cache-dir <dir>` | Cache directory | Platform default |
| `-f, --force` | Skip confirmation prompt | `false` |

---

### `purge` — Delete all cached models

```bash
hfdownload purge [options]
```

Deletes the **entire** cache directory contents. Prompts for confirmation unless `--force` is used.

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--cache-dir <dir>` | Cache directory | Platform default |
| `-f, --force` | Skip confirmation prompt | `false` |

**Example:**

```bash
hfdownload purge --force
```

---

### `config` — Show or modify configuration

#### `config show`

```bash
hfdownload config show
```

Displays all current configuration values and the config file location.

#### `config set`

```bash
hfdownload config set <key> <value>
```

**Available keys:**

| Key | Description | Default |
|-----|-------------|---------|
| `cache-dir` | Default cache directory | Platform default |
| `default-token` | Default Hugging Face auth token | (not set) |
| `default-revision` | Default Git revision | `main` |
| `no-progress` | Suppress progress bars by default | `false` |

**Example:**

```bash
hfdownload config set cache-dir /data/hf-models
hfdownload config set default-revision v2.0
hfdownload config set no-progress true
```

#### `config reset`

```bash
hfdownload config reset [--force]
```

Resets all configuration to defaults by deleting the config file.

---

## Authentication

The tool supports Hugging Face authentication for private and gated repositories:

1. **Environment variable** (recommended): Set `HF_TOKEN`
   ```bash
   export HF_TOKEN=hf_your_token_here
   ```

2. **Command-line option**: Use `--token` on the `download` command
   ```bash
   hfdownload download my-org/private-model model.bin --token hf_your_token
   ```

3. **Persistent config**: Store in configuration
   ```bash
   hfdownload config set default-token hf_your_token_here
   ```

## Cache Directory

By default, downloaded models are stored in a platform-specific cache directory:

| Platform | Default Path |
|----------|-------------|
| Windows | `%LOCALAPPDATA%\hfdownload\models` |
| Linux | `~/.local/share/hfdownload/models` |
| macOS | `~/Library/Application Support/hfdownload/models` |

Override with `--cache-dir` on any command, or set a default via `hfdownload config set cache-dir <path>`.

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Error (missing files, auth failure, not found, etc.) |

## Configuration File

The config file is stored at:

| Platform | Path |
|----------|------|
| Windows | `%APPDATA%\hfdownload\config.json` |
| Linux | `~/.config/hfdownload/config.json` |
| macOS | `~/.config/hfdownload/config.json` |
