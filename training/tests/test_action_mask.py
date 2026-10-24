"""Behavioral contract for the future discrete-action environment boundary."""

import pytest

from training.environments.action_mask import action_mask


def test_mask_preserves_stable_action_ids() -> None:
    assert action_mask(5, [0, 3]) == (1, 0, 0, 1, 0)


def test_empty_legal_actions_produces_zero_mask() -> None:
    assert action_mask(3, []) == (0, 0, 0)


def test_duplicate_ids_are_idempotent_and_iterators_work() -> None:
    assert action_mask(3, iter([2, 2, 0])) == (1, 0, 1)


@pytest.mark.parametrize("size", [0, -1])
def test_nonpositive_action_count_is_rejected(size: int) -> None:
    with pytest.raises(ValueError, match="positive"):
        action_mask(size, [])


@pytest.mark.parametrize("size", [True, 3.5, "3"])
def test_noninteger_action_count_is_rejected(size: object) -> None:
    with pytest.raises(TypeError, match="integer"):
        action_mask(size, [])  # type: ignore[arg-type]


@pytest.mark.parametrize("action", [-1, 3])
def test_out_of_range_action_is_rejected(action: int) -> None:
    with pytest.raises(ValueError, match="range"):
        action_mask(3, [action])


@pytest.mark.parametrize("action", [False, 1.5, "1"])
def test_noninteger_action_id_is_rejected(action: object) -> None:
    with pytest.raises(TypeError, match="integer"):
        action_mask(3, [action])  # type: ignore[list-item]
