"""Discrete legal-action encoding, independent of an engine transport."""

from collections.abc import Iterable


def action_mask(action_count: int, legal_actions: Iterable[int]) -> tuple[int, ...]:
    """Encode legal IDs; duplicates are harmless, invalid IDs fail explicitly.

    Empty legal actions yield an all-zero mask, including for inactive/terminal
    agents. The vocabulary size is positive even when no actions are legal.
    """
    if not isinstance(action_count, int) or isinstance(action_count, bool):
        raise TypeError("action_count must be an integer")
    if action_count <= 0:
        raise ValueError("action_count must be positive")

    mask = [0] * action_count
    for action in legal_actions:
        if not isinstance(action, int) or isinstance(action, bool):
            raise TypeError("action IDs must be integers")
        if not 0 <= action < action_count:
            raise ValueError("action ID is outside the vocabulary range")
        mask[action] = 1
    return tuple(mask)
