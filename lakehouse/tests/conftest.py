"""
Pytest configuration and local SparkSession fixture.
Provides isolated local SparkSession with Delta Lake support.
"""

import os
import sys

# Add lakehouse/src to sys.path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "src")))

try:
    import pytest

    @pytest.fixture(scope="session")
    def spark():
        from pyspark.sql import SparkSession
        return (
            SparkSession.builder
            .master("local[2]")
            .appName("GradCastLakehouseTests")
            .config("spark.sql.shuffle.partitions", "2")
            .config("spark.default.parallelism", "2")
            .config("spark.sql.extensions", "io.delta.sql.DeltaSparkSessionExtension")
            .config("spark.sql.catalog.spark_catalog", "org.apache.spark.sql.delta.catalog.DeltaCatalog")
            .getOrCreate()
        )
except ImportError:
    pass
