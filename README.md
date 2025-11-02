# MToolKit Runtime Modules

Core framework modules for production Unity games. Each module is documented with its own README.

## Overview

MToolKit provides essential systems required for shipped game titles. It's a modular framework built on dependency injection, reactive programming, and message-driven architecture.

## Modules

### Core System
- **Core** - Dependency injection, plugin architecture, configuration
- **MessageBus** - Decoupled message publishing/subscription
- **Bootstrapper** - Game bootstrapping with dependency preloading
- **Installer** - DI registration and service configuration

### UI & Interaction
- **Navigation** - View management, modals, subviews, canvas management
- **Components** - Reusable UI components (buttons, inputs, spinners, etc.)
- **Localization** - Multi-language support via Unity Localization

### Data & Persistence
- **Persistence** - Save system with ES3 integration, profiles, cloud backup
- **Settings** - Reactive settings system with UI binding

### Media & Content
- **Audio** - Audio playback service with mixer integration
- **Music** - Cross-scene music playback with crossfading
- **AssetLoader** - Addressables integration with parallel loading

### Services
- **Input** - Input abstraction with rebinding and device fallback
- **Analytics** - GameAnalytics integration with event tracking
- **ErrorSystem** - Global error handling with graceful degradation

### Infrastructure
- **Slog** - Structured logging with Serilog
- **Utilities** - Helper classes, extensions, and data structures

## Architecture Principles

1. **Loose Coupling** - Modules are independent and loosely coupled
2. **Dependency Injection** - VContainer-based DI for all services
3. **Reactive Programming** - R3 for reactive state management
4. **Event-Driven** - MessagePipe for decoupled communication
5. **Plugin Architecture** - Extensible plugin-based design

## Dependencies

- **VContainer** - Dependency injection
- **MessagePipe** - Async messaging
- **R3** - Reactive properties
- **Serilog** - Structured logging
- **ES3** - Save system
- **DOTween Pro** - Animation
- **Sirenix Odin Inspector** - Editor enhancements
- **Unity Input System** - Input handling
- **Unity Addressables** - Asset loading
- **Unity Localization** - Localization
- **GameAnalytics** - Analytics

## Current Status

**Status**: 15/17 core systems complete (93%)
- ✅ Complete: 15 systems
- 🚧 In Progress: 2 systems (Settings 95%, Accessibility)
- ❌ Missing: 0 critical gaps

