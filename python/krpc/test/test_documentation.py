#!/usr/bin/env python2

import unittest
import binascii
import krpc
import krpc.test.Test as TestSchema
from krpc.test.servertestcase import ServerTestCase

class TestClient(ServerTestCase, unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        super(TestClient, cls).setUpClass()

    @classmethod
    def tearDownClass(cls):
        super(TestClient, cls).tearDownClass()

    def setUp(self):
        super(TestClient, self).setUp()

    def test_basic(self):
        self.assertEqual('Service documentation string.', self.conn.test_service.__doc__)
        self.assertEqual('Procedure documentation string.', self.conn.test_service.float_to_string.__doc__)
        #self.assertEqual('Property documentation string.', self.conn.test_service.string_property.__doc__)
        #self.assertEqual('Class documentation string.', self.conn.test_service.TestClass.__doc__)
        obj = self.conn.test_service.create_test_object('Jeb')
        self.assertEqual('Method documentation string.', obj.get_value.__doc__)
        #self.assertEqual('Property documentation string.', obj.int_property.__doc__)

if __name__ == '__main__':
    unittest.main()
