﻿Feature: Callback
  In order to keep VH data up to date
  As an API service
  I want to handle external events

Scenario: Should accept and process a conference event request
	Given I have a conference
    And I have a valid conference event request
    When I send the request to the endpoint
    Then the response should have the status NoContent and success status True
	And the status is updated
