import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from categories import CANONICAL_CATEGORIES


def test_canonical_categories_is_list():
    assert isinstance(CANONICAL_CATEGORIES, list)


def test_canonical_categories_count():
    assert len(CANONICAL_CATEGORIES) == 26


def test_canonical_categories_contains_expected():
    assert "Casual" in CANONICAL_CATEGORIES
    assert "Shooter" in CANONICAL_CATEGORIES
    assert "Racing & Driving" in CANONICAL_CATEGORIES
    assert ".IO" in CANONICAL_CATEGORIES
    assert "Jigsaw" in CANONICAL_CATEGORIES


def test_canonical_categories_no_duplicates():
    assert len(CANONICAL_CATEGORIES) == len(set(CANONICAL_CATEGORIES))
