"""Tests for cancellation functionality."""

import asyncio

import pytest

from messageflow import CancellationToken, CancellationTokenSource


class TestCancellationToken:
    """Tests for CancellationToken."""

    def test_none_is_never_cancelled(self):
        """Test that CancellationToken.none() is never cancelled."""
        token = CancellationToken.none()
        assert not token.is_cancellation_requested()

        # Should not raise
        token.throw_if_cancellation_requested()

    def test_none_returns_same_instance(self):
        """Test that CancellationToken.none() returns the same instance."""
        token1 = CancellationToken.none()
        token2 = CancellationToken.none()
        assert token1 is token2

    def test_token_not_cancelled_initially(self):
        """Test that a new token is not cancelled initially."""
        source = CancellationTokenSource()
        token = source.token()
        assert not token.is_cancellation_requested()

        # Should not raise
        token.throw_if_cancellation_requested()

    def test_token_is_cancelled_after_source_cancel(self):
        """Test that a token is cancelled after its source is cancelled."""
        source = CancellationTokenSource()
        token = source.token()

        source.cancel()

        assert token.is_cancellation_requested()

    def test_throw_raises_when_cancelled(self):
        """Test that throw_if_cancellation_requested raises when cancelled."""
        source = CancellationTokenSource()
        token = source.token()

        source.cancel()

        with pytest.raises(asyncio.CancelledError) as exc_info:
            token.throw_if_cancellation_requested()

        assert "The operation was cancelled" in str(exc_info.value)

    def test_multiple_tokens_from_same_source(self):
        """Test that all tokens from the same source are cancelled together."""
        source = CancellationTokenSource()
        token1 = source.token()
        token2 = source.token()

        # Both tokens should reference the same underlying flag
        assert not token1.is_cancellation_requested()
        assert not token2.is_cancellation_requested()

        source.cancel()

        assert token1.is_cancellation_requested()
        assert token2.is_cancellation_requested()

    def test_cancel_is_idempotent(self):
        """Test that calling cancel() multiple times is safe."""
        source = CancellationTokenSource()
        token = source.token()

        source.cancel()
        source.cancel()
        source.cancel()

        assert token.is_cancellation_requested()

    def test_cancellation_does_not_affect_other_sources(self):
        """Test that cancelling one source doesn't affect other sources."""
        source1 = CancellationTokenSource()
        source2 = CancellationTokenSource()

        token1 = source1.token()
        token2 = source2.token()

        source1.cancel()

        assert token1.is_cancellation_requested()
        assert not token2.is_cancellation_requested()
