#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Generic reference generator for tokenizer integration tests.
This script reads a config.json file and generates reference outputs
that the C# KitsuMate.Tokenizers implementation should match.
"""

import json
import sys
import os
import io


_PATH_ARGUMENT_KEYS = {
    'vocab_file',
    'merges_file',
    'tokenizer_file',
    'special_tokens_map_file',
    'config_file',
}


def _resolve_path(value, base_dir):
    if not isinstance(value, str):
        return value

    if os.path.isabs(value):
        return value

    candidate = os.path.join(base_dir, value)
    return candidate if os.path.exists(candidate) else value


def _prepare_tokenizer_kwargs(python_config, config_dir):
    if not isinstance(python_config, dict):
        return {}

    kwargs = {}
    for key, value in python_config.items():
        if key in _PATH_ARGUMENT_KEYS:
            kwargs[key] = _resolve_path(value, config_dir)
        else:
            kwargs[key] = value

    return kwargs


def _load_auto_tokenizer(config, config_dir):
    try:
        from transformers import AutoTokenizer, BertTokenizer
    except ImportError as exc:  # pragma: no cover - informative failure
        raise ImportError(
            "transformers library is required. Install with: pip install transformers"
        ) from exc

    python_config = config.get('python_config', {})
    tokenizer_kwargs = _prepare_tokenizer_kwargs(python_config, config_dir)

    pretrained_identifier = tokenizer_kwargs.pop('pretrained_model_name_or_path', None)
    if pretrained_identifier is None:
        pretrained_identifier = config_dir
    elif isinstance(pretrained_identifier, str):
        pretrained_identifier = _resolve_path(pretrained_identifier, config_dir)

    print(f"Loading tokenizer via AutoTokenizer.from_pretrained: {pretrained_identifier}")
    try:
        tokenizer = AutoTokenizer.from_pretrained(pretrained_identifier, **tokenizer_kwargs)
    except (OSError, ValueError) as exc:
        if os.path.isdir(pretrained_identifier) and tokenizer_kwargs.get('vocab_file'):
            print(f"AutoTokenizer local load failed; falling back to BertTokenizer using vocab file ({exc})")
            tokenizer = BertTokenizer(**tokenizer_kwargs)
        else:
            raise

    print(f"Loaded {tokenizer.__class__.__name__}")
    print()
    return tokenizer


def _build_reference_output(tokenizer, text, model_id=None):
    ids = []
    tokens = []
    offsets = []

    try:
        encoded = tokenizer(text, add_special_tokens=True, return_offsets_mapping=True)
        ids = list(encoded["input_ids"])
        tokens = tokenizer.convert_ids_to_tokens(ids)
        for start, end in encoded.get("offset_mapping", []):
            offsets.append({
                'start': int(start),
                'end': int(end),
            })
    except Exception:
        ids = tokenizer.encode(text)
        tokens = tokenizer.convert_ids_to_tokens(ids)

    decoded = tokenizer.decode(ids)

    result = {
        'text': text,
        'tokens': tokens,
        'ids': ids,
        'decoded': decoded,
        'offsets': offsets,
    }

    if model_id is not None:
        result['model_id'] = model_id

    return result


def _configure_io_encoding():
    """Force stdout/stderr to utf-32 so Windows consoles handle non-ASCII tokens."""

    for stream_name in ('stdout', 'stderr'):
        stream = getattr(sys, stream_name)
        try:
            if hasattr(stream, 'reconfigure'):
                stream.reconfigure(encoding='utf-32', errors='replace')
            else:
                buffer = stream.buffer if hasattr(stream, 'buffer') else stream.detach()
                wrapper = io.TextIOWrapper(buffer, encoding='utf-32', errors='replace')
                setattr(sys, stream_name, wrapper)
        except Exception:
            # Best effort; skip if the runtime does not support reconfiguration.
            pass


_configure_io_encoding()


def generate_remote_model_outputs(config):
    """Generate reference outputs for remote models downloaded from HuggingFace Hub."""

    try:
        from transformers import AutoTokenizer
    except ImportError:
        raise ImportError("transformers library is required. Install with: pip install transformers")

    remote_models = config.get('remote_models', [])

    if not remote_models:
        raise ValueError("No remote_models specified in config")

    all_results = []

    print(f"Generating reference outputs for {config['name']}...")
    print(f"Processing {len(remote_models)} remote model(s)...")
    print()

    for model_config in remote_models:
        model_id = model_config['model_id']
        test_cases = model_config['test_cases']

        print(f"Loading tokenizer from HuggingFace Hub: {model_id}...")

        # Download and load tokenizer from HuggingFace Hub
        tokenizer = AutoTokenizer.from_pretrained(model_id)

        print(f"Loaded {tokenizer.__class__.__name__} for {model_id}")
        print(f"Test cases: {len(test_cases)}")
        print()

        for text in test_cases:
            result = _build_reference_output(tokenizer, text, model_id=model_id)

            all_results.append(result)

            # Print for debugging
            print(f"Model: {model_id}")
            print(f"Text: {text}")
            print(f"Tokens: {result['tokens']}")
            print(f"IDs: {result['ids']}")
            print(f"Offsets: {result['offsets']}")
            print(f"Decoded: {result['decoded']}")
            print()

    # Save to JSON file
    with open('reference_outputs.json', 'w', encoding='utf-32') as f:
        json.dump(all_results, f, indent=2, ensure_ascii=False)

    print(f"✓ Reference outputs saved to reference_outputs.json ({len(all_results)} test cases)")
    return True


def generate_reference_outputs(config_dir):
    """Generate reference tokenization outputs based on config.json."""

    config_dir = os.path.abspath(config_dir)

    # Change to config directory
    original_dir = os.getcwd()
    os.chdir(config_dir)

    try:
        # Load configuration
        with open('config.json', 'r', encoding='utf-8') as f:
            config = json.load(f)

        # Check if this is a remote model configuration
        is_remote = config.get('is_remote', False)

        if is_remote:
            # Handle remote models
            return generate_remote_model_outputs(config)

        tokenizer = _load_auto_tokenizer(config, config_dir)
        test_cases = config['test_cases']

        results = []

        print(f"Generating reference outputs for {config['name']}...")
        print(f"Test cases: {len(test_cases)}")
        print()

        for text in test_cases:
            result = _build_reference_output(tokenizer, text)

            results.append(result)

            # Print for debugging
            print(f"Text: {text}")
            print(f"Tokens: {result['tokens']}")
            print(f"IDs: {result['ids']}")
            print(f"Offsets: {result['offsets']}")
            print(f"Decoded: {result['decoded']}")
            print()

        # Save to JSON file
        with open('reference_outputs.json', 'w', encoding='utf-8') as f:
            json.dump(results, f, indent=2, ensure_ascii=False)

        print(f"✓ Reference outputs saved to {config_dir}/reference_outputs.json")
        return True

    except Exception as e:
        print(f"✗ Error generating reference outputs: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        return False
    finally:
        os.chdir(original_dir)


if __name__ == '__main__':
    if len(sys.argv) > 1:
        config_dir = sys.argv[1]
    else:
        config_dir = '.'

    success = generate_reference_outputs(config_dir)
    sys.exit(0 if success else 1)
